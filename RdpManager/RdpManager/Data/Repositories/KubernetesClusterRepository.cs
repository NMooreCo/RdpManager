using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using RdpManager.Models;

namespace RdpManager.Data.Repositories
{
    public class KubernetesClusterRepository : BaseRepository
    {
        public List<KubernetesCluster> GetAll()
        {
            const string sql = @"
                SELECT ClusterId, DisplayName, ProjectId, ClusterName, Region,
                       DefaultNamespace, ProxyAddress, ClearProxyBeforeAuth, CreatedAt, SortOrder
                FROM KubernetesClusters
                ORDER BY SortOrder, DisplayName";

            return ExecuteQuery(sql, MapFromReader);
        }

        public KubernetesCluster? GetById(Guid clusterId)
        {
            const string sql = @"
                SELECT ClusterId, DisplayName, ProjectId, ClusterName, Region,
                       DefaultNamespace, ProxyAddress, ClearProxyBeforeAuth, CreatedAt, SortOrder
                FROM KubernetesClusters
                WHERE ClusterId = @ClusterId";

            return ExecuteQuerySingle(sql, MapFromReader, new SQLiteParameter("@ClusterId", clusterId.ToString()));
        }

        public void Insert(KubernetesCluster cluster)
        {
            const string sql = @"
                INSERT INTO KubernetesClusters (ClusterId, DisplayName, ProjectId, ClusterName, Region,
                    DefaultNamespace, ProxyAddress, ClearProxyBeforeAuth, CreatedAt, SortOrder)
                VALUES (@ClusterId, @DisplayName, @ProjectId, @ClusterName, @Region,
                    @DefaultNamespace, @ProxyAddress, @ClearProxyBeforeAuth, @CreatedAt, @SortOrder)";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@ClusterId", cluster.ClusterId.ToString()),
                new SQLiteParameter("@DisplayName", cluster.DisplayName),
                new SQLiteParameter("@ProjectId", cluster.ProjectId),
                new SQLiteParameter("@ClusterName", cluster.ClusterName),
                new SQLiteParameter("@Region", cluster.Region),
                new SQLiteParameter("@DefaultNamespace", cluster.DefaultNamespace),
                new SQLiteParameter("@ProxyAddress", cluster.ProxyAddress ?? (object)DBNull.Value),
                new SQLiteParameter("@ClearProxyBeforeAuth", cluster.ClearProxyBeforeAuth ? 1 : 0),
                new SQLiteParameter("@CreatedAt", cluster.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                new SQLiteParameter("@SortOrder", cluster.SortOrder));
        }

        public void Update(KubernetesCluster cluster)
        {
            const string sql = @"
                UPDATE KubernetesClusters
                SET DisplayName = @DisplayName,
                    ProjectId = @ProjectId,
                    ClusterName = @ClusterName,
                    Region = @Region,
                    DefaultNamespace = @DefaultNamespace,
                    ProxyAddress = @ProxyAddress,
                    ClearProxyBeforeAuth = @ClearProxyBeforeAuth,
                    SortOrder = @SortOrder
                WHERE ClusterId = @ClusterId";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@ClusterId", cluster.ClusterId.ToString()),
                new SQLiteParameter("@DisplayName", cluster.DisplayName),
                new SQLiteParameter("@ProjectId", cluster.ProjectId),
                new SQLiteParameter("@ClusterName", cluster.ClusterName),
                new SQLiteParameter("@Region", cluster.Region),
                new SQLiteParameter("@DefaultNamespace", cluster.DefaultNamespace),
                new SQLiteParameter("@ProxyAddress", cluster.ProxyAddress ?? (object)DBNull.Value),
                new SQLiteParameter("@ClearProxyBeforeAuth", cluster.ClearProxyBeforeAuth ? 1 : 0),
                new SQLiteParameter("@SortOrder", cluster.SortOrder));
        }

        public void Delete(Guid clusterId)
        {
            const string sql = "DELETE FROM KubernetesClusters WHERE ClusterId = @ClusterId";
            ExecuteNonQuery(sql, new SQLiteParameter("@ClusterId", clusterId.ToString()));
        }

        private KubernetesCluster MapFromReader(IDataReader reader)
        {
            return new KubernetesCluster
            {
                ClusterId = Guid.Parse(reader.GetString(reader.GetOrdinal("ClusterId"))),
                DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
                ProjectId = reader.GetString(reader.GetOrdinal("ProjectId")),
                ClusterName = reader.GetString(reader.GetOrdinal("ClusterName")),
                Region = reader.GetString(reader.GetOrdinal("Region")),
                DefaultNamespace = reader.GetString(reader.GetOrdinal("DefaultNamespace")),
                ProxyAddress = GetString(reader, "ProxyAddress"),
                ClearProxyBeforeAuth = GetBool(reader, "ClearProxyBeforeAuth"),
                CreatedAt = DateTime.Parse(GetString(reader, "CreatedAt") ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
                SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder"))
            };
        }
    }
}
