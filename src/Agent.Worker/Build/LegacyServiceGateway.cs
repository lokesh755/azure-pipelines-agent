using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public sealed class LegacyServiceGateway : BaseServiceGateway
    {
        public override string System => "";

        public override Type ExtensionType => typeof(IServiceGateway);

        public override async Task AssociateArtifactAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string name,
            string type,
            string data,
            Dictionary<string, string> propertiesDictionary,
            CancellationToken cancellationToken)
        {
            BuildServer buildHelper = new BuildServer(connection, projectId);
            var artifact = await buildHelper.AssociateArtifact(buildId, name, type, data, propertiesDictionary, cancellationToken);
            context.Output(StringUtil.Loc("AssociateArtifactWithBuild", artifact.Id, buildId));
        }

        public override async Task UploadArtifactAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            long containerId,
            string containerPath,
            int buildId,
            string name,
            Dictionary<string, string> propertiesDictionary,
            string source,
            CancellationToken cancellationToken)
        {
            var fileContainerFullPath = await base.CopyArtifactAsync(
                context,
                connection,
                projectId,
                containerId,
                containerPath,
                buildId,
                name,
                propertiesDictionary,
                source,
                cancellationToken);

            await AssociateArtifactAsync(
                context,
                connection,
                projectId,
                buildId,
                name,
                WellKnownArtifactResourceTypes.Container,
                fileContainerFullPath,
                propertiesDictionary,
                cancellationToken);
        }

        public override async Task UpdateBuildNumberAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string buildNumber,
            CancellationToken cancellationToken)
        {
            BuildServer buildServer = new BuildServer(connection, projectId);
            var build = await buildServer.UpdateBuildNumber(buildId, buildNumber, cancellationToken);
            context.Output(StringUtil.Loc("UpdateBuildNumberForBuild", build.BuildNumber, build.Id));
        }

        public override async Task AddBuildTagAsync(
            IAsyncCommandContext context,
            VssConnection connection,
            Guid projectId,
            int buildId,
            string buildTag,
            CancellationToken cancellationToken)
        {
            BuildServer buildServer = new BuildServer(connection, projectId);
            var tags = await buildServer.AddBuildTag(buildId, buildTag, cancellationToken);

            if (tags == null || !tags.Contains(buildTag))
            {
                throw new Exception(StringUtil.Loc("BuildTagAddFailed", buildTag));
            }
            else
            {
                context.Output(StringUtil.Loc("BuildTagsForBuild", buildId, String.Join(", ", tags)));
            }
        }

        public override async Task<List<AgentBuildArtifact>> GetArtifacts(VssConnection connection, int buildId, Guid projectId)
        {
            BuildServer buildServer = new BuildServer(connection, projectId);
            var buildArtifacts = await buildServer.GetArtifacts(buildId);
            return ToAgentBuildArtifact(buildArtifacts);
        }

        private AgentArtifactResource ToAgentArtifactResource(ArtifactResource resource)
        {
            return new AgentArtifactResource
            {
                Data = resource.Data,
                DownloadUrl = resource.DownloadUrl,
                Properties = resource.Properties,
                Type = resource.Type,
                Url = resource.Url
            };
        }

        private List<AgentBuildArtifact> ToAgentBuildArtifact(List<BuildArtifact> artifacts)
        {
            return artifacts.Select(
                artifact => new AgentBuildArtifact
                {
                    Id = artifact.Id,
                    Name = artifact.Name,
                    Resource = ToAgentArtifactResource(artifact.Resource)
                }).ToList();
        }
    }

    public class AgentBuildArtifact
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public AgentArtifactResource Resource { get; set; }
    }

    public class AgentArtifactResource
    {
        private ReferenceLinks m_links;

        public string Type { get; set; }

        public string Data { get; set; }

        public Dictionary<string, string> Properties { get; set; }

        public string Url { get; set; }

        public string DownloadUrl { get; set; }

        public ReferenceLinks Links
        {
            get
            {
                if (this.m_links == null)
                    this.m_links = new ReferenceLinks();
                return this.m_links;
            }
        }
    }
}