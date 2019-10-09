using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Elsa.Models;
using Elsa.Persistence;

namespace Elsa.Services
{
    public class WorkflowPublisher : IWorkflowPublisher
    {
        private readonly IWorkflowDefinitionStore store;
        private readonly IIdGenerator idGenerator;
        private readonly IMapper mapper;

        public WorkflowPublisher(
            IWorkflowDefinitionStore store,
            IIdGenerator idGenerator,
            IMapper mapper)
        {
            this.store = store;
            this.idGenerator = idGenerator;
            this.mapper = mapper;
        }

        public WorkflowDefinitionVersion New()
        {
            var definition = new WorkflowDefinitionVersion
            {
                Id = idGenerator.Generate(),
                DefinitionId = idGenerator.Generate(),
                Name = "New Workflow",
                Version = 1,
                IsLatest = true,
                IsPublished = false,
                IsSingleton = false,
                IsDisabled = false
            };

            return definition;
        }

        public async Task<WorkflowDefinitionVersion> PublishAsync(
            string id,
            CancellationToken cancellationToken)
        {
            var definition = await store.GetByIdAsync(id, VersionOptions.Latest, cancellationToken);

            if (definition == null)
                return null;

            return await PublishAsync(definition, cancellationToken);
        }

        public async Task<WorkflowDefinitionVersion> PublishAsync(
            WorkflowDefinitionVersion workflowDefinition,
            CancellationToken cancellationToken)
        {
            var definition = mapper.Map<WorkflowDefinitionVersion>(workflowDefinition);

            var publishedDefinition = await store.GetByIdAsync(
                definition.DefinitionId,
                VersionOptions.Published,
                cancellationToken);

            if (publishedDefinition != null)
            {
                publishedDefinition.IsPublished = false;
                publishedDefinition.IsLatest = false;
                await store.UpdateAsync(publishedDefinition, cancellationToken);
            }

            definition.IsPublished = true;
            definition.IsLatest = true;
            await store.SaveAsync(definition, cancellationToken);

            return definition;
        }

        public async Task<WorkflowDefinitionVersion> GetDraftAsync(
            string id,
            CancellationToken cancellationToken)
        {
            var definition = await store.GetByIdAsync(id, VersionOptions.Latest, cancellationToken);

            if (definition == null)
                return null;

            if (!definition.IsPublished)
                return definition;

            var draft = mapper.Map<WorkflowDefinitionVersion>(definition);
            draft.Id = idGenerator.Generate();
            draft.IsPublished = false;
            draft.IsLatest = true;
            draft.Version++;

            return draft;
        }

        public async Task<WorkflowDefinitionVersion> SaveDraftAsync(
            WorkflowDefinitionVersion draft,
            CancellationToken cancellationToken)
        {
            // Mark currently published version as non-latest.
            var latestVersion = await store.GetByIdAsync(
                draft.DefinitionId,
                VersionOptions.Latest,
                cancellationToken);

            if (latestVersion != null && latestVersion.IsPublished && latestVersion.IsLatest)
            {
                latestVersion.IsLatest = false;
                await store.UpdateAsync(latestVersion, cancellationToken);
            }

            // Store the updated draft.
            await store.SaveAsync(draft, cancellationToken);

            return draft;
        }
    }
}