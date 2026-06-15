using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RdpManager.Models;

namespace RdpManager.Services
{
    public class KubernetesService
    {
        private static string? _gcloudPath;
        private static string? _kubectlPath;
        private static readonly string KubeconfigDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RdpManager", "kubeconfigs");

        /// <summary>
        /// Resolve a CLI tool path. On Windows, gcloud is a .cmd batch file
        /// so Process.Start can't find it without shell execution.
        /// We use cmd /c where to find the full path.
        /// </summary>
        private static string ResolveTool(string tool)
        {
            // On Windows, gcloud is a .cmd batch file — we must find the .cmd/.bat/.exe
            // extension because Process.Start cannot run extensionless shell scripts.
            var pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
            var extensions = pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var dir in paths)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;

                // Only try with extensions — never the bare name (it may be a
                // bash script that Windows can't execute directly)
                foreach (var ext in extensions)
                {
                    var withExt = System.IO.Path.Combine(dir, tool + ext);
                    if (System.IO.File.Exists(withExt))
                        return withExt;
                }
            }

            // Fallback: return the tool name and hope for the best
            return tool;
        }

        private static string GcloudPath => _gcloudPath ??= ResolveTool("gcloud");
        private static string KubectlPath => _kubectlPath ??= ResolveTool("kubectl");

        /// <summary>
        /// Get isolated kubeconfig file path for a cluster.
        /// Each cluster writes to its own kubeconfig so we never touch ~/.kube/config.
        /// </summary>
        public static string GetClusterKubeconfigPath(KubernetesCluster cluster)
        {
            System.IO.Directory.CreateDirectory(KubeconfigDir);
            return System.IO.Path.Combine(KubeconfigDir, $"{cluster.ClusterId}.config");
        }

        /// <summary>
        /// Run gcloud auth login interactively (opens browser).
        /// Returns true if the process exited successfully.
        /// </summary>
        public async Task<(bool Success, string Error)> GcloudAuthLoginAsync()
        {
            try
            {
                var psi = new ProcessStartInfo(GcloudPath, "auth login")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                };

                using var process = new Process { StartInfo = psi };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                return process.ExitCode == 0
                    ? (true, string.Empty)
                    : (false, errorTask.Result);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Authenticate to a GKE cluster using the critical proxy flow:
        /// 1. Clear HTTPS_PROXY
        /// 2. Run gcloud get-credentials
        /// 3. Subsequent kubectl calls use the proxy per-process
        /// </summary>
        public async Task<(bool Success, string Output, string Error)> AuthenticateClusterAsync(KubernetesCluster cluster)
        {
            var envOverrides = new Dictionary<string, string>();

            // Use isolated kubeconfig so we never touch ~/.kube/config
            envOverrides["KUBECONFIG"] = GetClusterKubeconfigPath(cluster);

            // Clear proxy before auth if configured
            if (cluster.ClearProxyBeforeAuth)
            {
                envOverrides["HTTPS_PROXY"] = "";
                envOverrides["https_proxy"] = "";
                envOverrides["HTTP_PROXY"] = "";
                envOverrides["http_proxy"] = "";
            }

            var args = $"container clusters get-credentials {cluster.ClusterName} --region {cluster.Region} --project {cluster.ProjectId}";
            var result = await RunProcessAsync(GcloudPath, args, envOverrides);

            return (result.ExitCode == 0, result.Output, result.Error);
        }

        /// <summary>
        /// Get pods in a namespace
        /// </summary>
        public async Task<(List<KubernetesPod> Pods, string Error)> GetPodsAsync(KubernetesCluster cluster, string @namespace)
        {
            var result = await RunKubectlAsync(cluster, $"get pods -n {@namespace} -o json");

            if (result.ExitCode != 0)
                return (new List<KubernetesPod>(), result.Error);

            try
            {
                var json = JObject.Parse(result.Output);
                var pods = new List<KubernetesPod>();

                foreach (var item in json["items"] ?? new JArray())
                {
                    var metadata = item["metadata"];
                    var status = item["status"];
                    var spec = item["spec"];

                    var containerStatuses = status?["containerStatuses"] as JArray;
                    var readyCount = containerStatuses?.Count(c => c["ready"]?.Value<bool>() == true) ?? 0;
                    var totalCount = containerStatuses?.Count ?? spec?["containers"]?.Count() ?? 0;
                    var restarts = containerStatuses?.Sum(c => c["restartCount"]?.Value<int>() ?? 0) ?? 0;

                    var containers = spec?["containers"]?.Select(c => c["name"]?.Value<string>() ?? "").ToList() ?? new List<string>();

                    var creationTimestamp = metadata?["creationTimestamp"]?.Value<DateTime>();
                    var age = creationTimestamp.HasValue ? FormatAge(DateTime.UtcNow - creationTimestamp.Value) : "Unknown";

                    pods.Add(new KubernetesPod
                    {
                        Name = metadata?["name"]?.Value<string>() ?? "",
                        Namespace = metadata?["namespace"]?.Value<string>() ?? @namespace,
                        Status = GetPodPhase(status),
                        Ready = $"{readyCount}/{totalCount}",
                        Restarts = restarts,
                        Age = age,
                        Node = spec?["nodeName"]?.Value<string>() ?? "",
                        IP = status?["podIP"]?.Value<string>() ?? "",
                        Containers = containers,
                        Cluster = cluster
                    });
                }

                return (pods, string.Empty);
            }
            catch (Exception ex)
            {
                return (new List<KubernetesPod>(), $"Failed to parse pod data: {ex.Message}");
            }
        }

        /// <summary>
        /// Get namespaces for a cluster
        /// </summary>
        public async Task<(List<string> Namespaces, string Error)> GetNamespacesAsync(KubernetesCluster cluster)
        {
            var result = await RunKubectlAsync(cluster, "get namespaces -o json");

            if (result.ExitCode != 0)
                return (new List<string>(), result.Error);

            try
            {
                var json = JObject.Parse(result.Output);
                var namespaces = json["items"]?
                    .Select(item => item["metadata"]?["name"]?.Value<string>() ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .OrderBy(n => n)
                    .ToList() ?? new List<string>();

                return (namespaces, string.Empty);
            }
            catch (Exception ex)
            {
                return (new List<string>(), $"Failed to parse namespace data: {ex.Message}");
            }
        }

        /// <summary>
        /// Stream pod logs with a callback per line
        /// </summary>
        public async Task StreamPodLogsAsync(KubernetesCluster cluster, KubernetesPod pod,
            string? container, Action<string> onLine, CancellationToken cancellationToken)
        {
            var containerArg = !string.IsNullOrEmpty(container) ? $"-c {container}" : "";
            var args = $"logs -f --tail=200 -n {pod.Namespace} {pod.Name} {containerArg}";

            var psi = CreateKubectlProcessStartInfo(cluster, args);
            using var process = new Process { StartInfo = psi };

            process.Start();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await process.StandardOutput.ReadLineAsync();
                    if (line == null) break;
                    onLine(line);
                }
            }
            finally
            {
                if (!process.HasExited)
                {
                    try { process.Kill(); } catch { }
                }
            }
        }

        /// <summary>
        /// Get pod logs (static fetch)
        /// </summary>
        public async Task<(string Logs, string Error)> GetPodLogsAsync(KubernetesCluster cluster,
            KubernetesPod pod, string? container, int tailLines = 200)
        {
            var containerArg = !string.IsNullOrEmpty(container) ? $"-c {container}" : "";
            var result = await RunKubectlAsync(cluster, $"logs --tail={tailLines} -n {pod.Namespace} {pod.Name} {containerArg}");

            return result.ExitCode == 0 ? (result.Output, string.Empty) : (string.Empty, result.Error);
        }

        /// <summary>
        /// Get previous container logs (for crashed/restarted containers)
        /// </summary>
        public async Task<(string Logs, string Error)> GetPreviousPodLogsAsync(KubernetesCluster cluster,
            KubernetesPod pod, string? container, int tailLines = 200)
        {
            var containerArg = !string.IsNullOrEmpty(container) ? $"-c {container}" : "";
            var result = await RunKubectlAsync(cluster, $"logs --previous --tail={tailLines} -n {pod.Namespace} {pod.Name} {containerArg}");

            return result.ExitCode == 0 ? (result.Output, string.Empty) : (string.Empty, result.Error);
        }

        /// <summary>
        /// Describe a pod (returns raw text)
        /// </summary>
        public async Task<(string Description, string Error)> DescribePodAsync(KubernetesCluster cluster, KubernetesPod pod)
        {
            var result = await RunKubectlAsync(cluster, $"describe pod -n {pod.Namespace} {pod.Name}");
            return result.ExitCode == 0 ? (result.Output, string.Empty) : (string.Empty, result.Error);
        }

        /// <summary>
        /// Get deployments in a namespace
        /// </summary>
        public async Task<(List<KubernetesDeployment> Deployments, string Error)> GetDeploymentsAsync(
            KubernetesCluster cluster, string @namespace)
        {
            var result = await RunKubectlAsync(cluster, $"get deployments -n {@namespace} -o json");

            if (result.ExitCode != 0)
                return (new List<KubernetesDeployment>(), result.Error);

            try
            {
                var json = JObject.Parse(result.Output);
                var deployments = new List<KubernetesDeployment>();

                foreach (var item in json["items"] ?? new JArray())
                {
                    var metadata = item["metadata"];
                    var spec = item["spec"];
                    var status = item["status"];

                    var creationTimestamp = metadata?["creationTimestamp"]?.Value<DateTime>();
                    var age = creationTimestamp.HasValue ? FormatAge(DateTime.UtcNow - creationTimestamp.Value) : "Unknown";

                    deployments.Add(new KubernetesDeployment
                    {
                        Name = metadata?["name"]?.Value<string>() ?? "",
                        Namespace = metadata?["namespace"]?.Value<string>() ?? @namespace,
                        Replicas = spec?["replicas"]?.Value<int>() ?? 0,
                        ReadyReplicas = status?["readyReplicas"]?.Value<int>() ?? 0,
                        AvailableReplicas = status?["availableReplicas"]?.Value<int>() ?? 0,
                        UpdatedReplicas = status?["updatedReplicas"]?.Value<int>() ?? 0,
                        Age = age,
                        Strategy = spec?["strategy"]?["type"]?.Value<string>() ?? "",
                        Cluster = cluster
                    });
                }

                return (deployments, string.Empty);
            }
            catch (Exception ex)
            {
                return (new List<KubernetesDeployment>(), $"Failed to parse deployment data: {ex.Message}");
            }
        }

        /// <summary>
        /// Scale a deployment
        /// </summary>
        public async Task<(bool Success, string Output, string Error)> ScaleDeploymentAsync(
            KubernetesCluster cluster, string @namespace, string deploymentName, int replicas)
        {
            var result = await RunKubectlAsync(cluster, $"scale deployment {deploymentName} -n {@namespace} --replicas={replicas}");
            return (result.ExitCode == 0, result.Output, result.Error);
        }

        /// <summary>
        /// Restart a deployment (rolling restart)
        /// </summary>
        public async Task<(bool Success, string Output, string Error)> RestartDeploymentAsync(
            KubernetesCluster cluster, string @namespace, string deploymentName)
        {
            var result = await RunKubectlAsync(cluster, $"rollout restart deployment {deploymentName} -n {@namespace}");
            return (result.ExitCode == 0, result.Output, result.Error);
        }

        /// <summary>
        /// Get rollout status for a deployment
        /// </summary>
        public async Task<(string Status, string Error)> GetRolloutStatusAsync(
            KubernetesCluster cluster, string @namespace, string deploymentName)
        {
            var result = await RunKubectlAsync(cluster, $"rollout status deployment {deploymentName} -n {@namespace}");
            return result.ExitCode == 0 ? (result.Output, string.Empty) : (string.Empty, result.Error);
        }

        /// <summary>
        /// Get rollout history for a deployment
        /// </summary>
        public async Task<(string History, string Error)> GetRolloutHistoryAsync(
            KubernetesCluster cluster, string @namespace, string deploymentName)
        {
            var result = await RunKubectlAsync(cluster, $"rollout history deployment {deploymentName} -n {@namespace}");
            return result.ExitCode == 0 ? (result.Output, string.Empty) : (string.Empty, result.Error);
        }

        /// <summary>
        /// Delete a pod (K8s will auto-recreate from deployment)
        /// </summary>
        public async Task<(bool Success, string Output, string Error)> DeletePodAsync(
            KubernetesCluster cluster, KubernetesPod pod)
        {
            var result = await RunKubectlAsync(cluster, $"delete pod {pod.Name} -n {pod.Namespace}");
            return (result.ExitCode == 0, result.Output, result.Error);
        }

        /// <summary>
        /// Get cluster events
        /// </summary>
        public async Task<(List<KubernetesEvent> Events, string Error)> GetEventsAsync(
            KubernetesCluster cluster, string @namespace)
        {
            var nsArg = string.IsNullOrEmpty(@namespace) ? "--all-namespaces" : $"-n {@namespace}";
            var result = await RunKubectlAsync(cluster, $"get events {nsArg} --sort-by=.lastTimestamp -o json");

            if (result.ExitCode != 0)
                return (new List<KubernetesEvent>(), result.Error);

            try
            {
                var json = JObject.Parse(result.Output);
                var events = new List<KubernetesEvent>();

                foreach (var item in json["items"] ?? new JArray())
                {
                    var involvedObject = item["involvedObject"];
                    var objectRef = $"{involvedObject?["kind"]?.Value<string>()}/{involvedObject?["name"]?.Value<string>()}";

                    events.Add(new KubernetesEvent
                    {
                        Type = item["type"]?.Value<string>() ?? "Normal",
                        Reason = item["reason"]?.Value<string>() ?? "",
                        Message = item["message"]?.Value<string>() ?? "",
                        Object = objectRef,
                        Timestamp = item["lastTimestamp"]?.Value<DateTime>(),
                        Count = item["count"]?.Value<int>() ?? 1
                    });
                }

                return (events, string.Empty);
            }
            catch (Exception ex)
            {
                return (new List<KubernetesEvent>(), $"Failed to parse events: {ex.Message}");
            }
        }

        /// <summary>
        /// Get pod resource usage (CPU/Memory)
        /// </summary>
        public async Task<(List<KubernetesResource> Resources, string Error)> GetTopPodsAsync(
            KubernetesCluster cluster, string @namespace)
        {
            var result = await RunKubectlAsync(cluster, $"top pods -n {@namespace} --no-headers");

            if (result.ExitCode != 0)
                return (new List<KubernetesResource>(), result.Error);

            var resources = new List<KubernetesResource>();
            foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    resources.Add(new KubernetesResource
                    {
                        PodName = parts[0],
                        Namespace = @namespace,
                        CpuUsage = parts[1],
                        MemoryUsage = parts[2]
                    });
                }
            }

            return (resources, string.Empty);
        }

        /// <summary>
        /// Get services in a namespace
        /// </summary>
        public async Task<(string ServicesJson, string Error)> GetServicesAsync(
            KubernetesCluster cluster, string @namespace)
        {
            var result = await RunKubectlAsync(cluster, $"get svc -n {@namespace} -o json");
            return result.ExitCode == 0 ? (result.Output, string.Empty) : (string.Empty, result.Error);
        }

        /// <summary>
        /// Start port forwarding (returns the Process so caller can manage lifecycle)
        /// </summary>
        public Process? StartPortForward(KubernetesCluster cluster, KubernetesPod pod,
            int localPort, int remotePort)
        {
            var psi = CreateKubectlProcessStartInfo(cluster,
                $"port-forward -n {pod.Namespace} {pod.Name} {localPort}:{remotePort}");

            var process = new Process { StartInfo = psi };
            process.Start();
            return process;
        }

        /// <summary>
        /// Start an exec process into a pod (returns Process for terminal control)
        /// </summary>
        public Process? StartPodExecProcess(KubernetesCluster cluster, KubernetesPod pod,
            string? container = null, string shell = "sh")
        {
            var containerArg = !string.IsNullOrEmpty(container) ? $"-c {container}" : "";
            var psi = CreateKubectlProcessStartInfo(cluster,
                $"exec -i -n {pod.Namespace} {pod.Name} {containerArg} -- {shell}");

            // For exec, we need stdin
            psi.RedirectStandardInput = true;

            var process = new Process { StartInfo = psi };
            process.Start();
            return process;
        }

        /// <summary>
        /// Copy file from pod to local
        /// </summary>
        public async Task<(bool Success, string Error)> CopyFromPodAsync(
            KubernetesCluster cluster, KubernetesPod pod, string remotePath, string localPath)
        {
            var result = await RunKubectlAsync(cluster, $"cp {pod.Namespace}/{pod.Name}:{remotePath} \"{localPath}\"");
            return (result.ExitCode == 0, result.Error);
        }

        /// <summary>
        /// Copy file to pod from local
        /// </summary>
        public async Task<(bool Success, string Error)> CopyToPodAsync(
            KubernetesCluster cluster, KubernetesPod pod, string localPath, string remotePath)
        {
            var result = await RunKubectlAsync(cluster, $"cp \"{localPath}\" {pod.Namespace}/{pod.Name}:{remotePath}");
            return (result.ExitCode == 0, result.Error);
        }

        /// <summary>
        /// Get config maps in a namespace
        /// </summary>
        public async Task<(string ConfigMapsJson, string Error)> GetConfigMapsAsync(
            KubernetesCluster cluster, string @namespace)
        {
            var result = await RunKubectlAsync(cluster, $"get configmap -n {@namespace} -o json");
            return result.ExitCode == 0 ? (result.Output, string.Empty) : (string.Empty, result.Error);
        }

        /// <summary>
        /// Get secret names (not values) in a namespace
        /// </summary>
        public async Task<(string SecretsJson, string Error)> GetSecretsAsync(
            KubernetesCluster cluster, string @namespace)
        {
            // Only get metadata, not the actual secret values
            var result = await RunKubectlAsync(cluster, $"get secrets -n {@namespace} -o json");
            return result.ExitCode == 0 ? (result.Output, string.Empty) : (string.Empty, result.Error);
        }

        #region Process Helpers

        private ProcessStartInfo CreateKubectlProcessStartInfo(KubernetesCluster cluster, string arguments)
        {
            var psi = new ProcessStartInfo(KubectlPath, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Use isolated kubeconfig so we never touch ~/.kube/config
            psi.Environment["KUBECONFIG"] = GetClusterKubeconfigPath(cluster);

            // Set proxy for kubectl calls if configured
            if (!string.IsNullOrEmpty(cluster.ProxyAddress))
            {
                psi.Environment["HTTPS_PROXY"] = cluster.ProxyAddress;
                psi.Environment["https_proxy"] = cluster.ProxyAddress;
                psi.Environment["HTTP_PROXY"] = cluster.ProxyAddress;
                psi.Environment["http_proxy"] = cluster.ProxyAddress;
            }

            return psi;
        }

        private async Task<(string Output, string Error, int ExitCode)> RunKubectlAsync(
            KubernetesCluster cluster, string arguments)
        {
            var psi = CreateKubectlProcessStartInfo(cluster, arguments);
            return await RunProcessInternalAsync(psi);
        }

        private async Task<(string Output, string Error, int ExitCode)> RunProcessAsync(
            string fileName, string arguments, Dictionary<string, string>? environmentOverrides = null)
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (environmentOverrides != null)
            {
                foreach (var kvp in environmentOverrides)
                {
                    psi.Environment[kvp.Key] = kvp.Value;
                }
            }

            return await RunProcessInternalAsync(psi);
        }

        private static async Task<(string Output, string Error, int ExitCode)> RunProcessInternalAsync(ProcessStartInfo psi)
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            // Read both streams concurrently to avoid deadlocks
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync();

            return (outputTask.Result, errorTask.Result, process.ExitCode);
        }

        private static string GetPodPhase(JToken? status)
        {
            var phase = status?["phase"]?.Value<string>() ?? "Unknown";

            // Check container statuses for more specific state
            var containerStatuses = status?["containerStatuses"] as JArray;
            if (containerStatuses != null)
            {
                foreach (var cs in containerStatuses)
                {
                    var waiting = cs["state"]?["waiting"];
                    if (waiting != null)
                    {
                        var reason = waiting["reason"]?.Value<string>();
                        if (!string.IsNullOrEmpty(reason))
                            return reason; // e.g., CrashLoopBackOff, ImagePullBackOff
                    }
                }
            }

            // Check if terminating
            if (status?.Parent?.Parent?["metadata"]?["deletionTimestamp"] != null)
                return "Terminating";

            return phase;
        }

        private static string FormatAge(TimeSpan age)
        {
            if (age.TotalDays >= 1)
                return $"{(int)age.TotalDays}d";
            if (age.TotalHours >= 1)
                return $"{(int)age.TotalHours}h";
            if (age.TotalMinutes >= 1)
                return $"{(int)age.TotalMinutes}m";
            return $"{(int)age.TotalSeconds}s";
        }

        #endregion
    }
}
