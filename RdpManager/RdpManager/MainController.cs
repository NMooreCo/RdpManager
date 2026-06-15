using RdpManager.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Automation;

namespace RdpManager
{
    static class MainController
    {
        public static NetworkCredential LoadCredentials(string target)
        {
            using (var cred = new Credential { Target = target })
            {

                if (cred.Load())
                {
                    return new NetworkCredential(cred.Username, cred.Password);
                }
                else
                {
                    return null;
                }
            }
        }

        public static void ConnectToMachine(string machineAddress, string? friendlyName = null)
        {
            // Load credentials from secure storage
            var creds = LoadCredentials("RdpManagerCredentials");
            if (creds == null)
            {
                System.Windows.MessageBox.Show("Please set your username and password in the settings.");
                return;
            }

            string rdpFile = CreateRdpFile(machineAddress, creds.UserName);

            // Start mstsc.exe with the RDP file
            var process = Process.Start("mstsc.exe", $"\"{rdpFile}\"");

            // Start UI Automation in a separate thread
            Thread automationThread = new Thread(() => AutomateRdpLogin(creds.Password));
            automationThread?.SetApartmentState(ApartmentState.STA);
            automationThread?.Start();
        }

        private static void AutomateRdpLogin(string password)
        {
            try
            {
                // Wait for the credential window to appear
                AutomationElement loginWindow = WaitForElement(
                    () => AutomationElement.RootElement.FindFirst(
                        TreeScope.Children,
                        new PropertyCondition(AutomationElement.NameProperty, "Windows Security")),
                    TimeSpan.FromSeconds(10));

                if (loginWindow == null)
                {
                    //System.Windows.MessageBox.Show("Failed to find the credential window.");
                    return;
                }

                // Find the password box
                AutomationElement passwordBox = loginWindow.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));

                if (passwordBox == null)
                {
                    //System.Windows.MessageBox.Show("Failed to find the password box.");
                    return;
                }

                // Set the password
                var valuePattern = passwordBox.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                valuePattern?.SetValue(password);

                // Find the OK button
                AutomationElement okButton = loginWindow.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, "OK"));

                if (okButton == null)
                {
                    //System.Windows.MessageBox.Show("Failed to find the OK button.");
                    return;
                }

                // Invoke the OK button
                var invokePattern = okButton.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                invokePattern?.Invoke();
            }
            catch (Exception)
            {
                // Ignore exception
            }
        }

        private static AutomationElement WaitForElement(Func<AutomationElement> findElement, TimeSpan timeout)
        {
            AutomationElement element = default;
            var stopwatch = Stopwatch.StartNew();

            while (element == null && stopwatch.Elapsed < timeout)
            {
                element = findElement();
                Thread.Sleep(500);
            }

            return element;
        }

        public static string CreateRdpFile(string machineAddress, string username)
        {
            string rdpContent = $@"
screen mode id:i:2
use multimon:i:0
desktopwidth:i:{SystemParameters.PrimaryScreenWidth}
desktopheight:i:{SystemParameters.PrimaryScreenHeight}
session bpp:i:32
full address:s:{machineAddress}
username:s:{username}
prompt for credentials:i:1
";
            string filePath = Path.Combine(Path.GetTempPath(), $"{machineAddress}.rdp");
            File.WriteAllText(filePath, rdpContent);
            return filePath;
        }

        public static void CopyPasswordToClipboard(string password)
        {
            // Ensure the thread is in STA mode
            Thread clipboardThread = new Thread(() => System.Windows.Clipboard.SetText(password));
            clipboardThread?.SetApartmentState(ApartmentState.STA);
            clipboardThread?.Start();
            clipboardThread?.Join();
        }

        public static void SaveCredentials(string target, string username, string password)
        {
            using (var cred = new Credential())
            {
                cred.Password = password;
                cred.Username = username;
                cred.Target = target;
                cred.Type = CredentialType.Generic;
                cred.PersistanceType = PersistanceType.LocalComputer;
                cred.Save();
            }
        }
    }
}
