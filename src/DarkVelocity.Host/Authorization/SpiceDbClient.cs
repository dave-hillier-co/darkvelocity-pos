using Authzed.Api.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;

namespace DarkVelocity.Host.Authorization;

public interface ISpiceDbClient
{
    Task<bool> CheckPermissionAsync(
        string resourceType,
        string resourceId,
        string permission,
        string subjectType,
        string subjectId,
        CancellationToken cancellationToken = default);

    Task WriteRelationshipAsync(
        string resourceType,
        string resourceId,
        string relation,
        string subjectType,
        string subjectId,
        string? caveatName = null,
        Dictionary<string, object>? caveatContext = null,
        CancellationToken cancellationToken = default);

    Task DeleteRelationshipAsync(
        string resourceType,
        string resourceId,
        string relation,
        string subjectType,
        string subjectId,
        CancellationToken cancellationToken = default);

    Task<List<string>> LookupResourcesAsync(
        string resourceType,
        string permission,
        string subjectType,
        string subjectId,
        CancellationToken cancellationToken = default);
}

public sealed class SpiceDbClient : ISpiceDbClient
{
    private readonly PermissionsService.PermissionsServiceClient _client;
    private readonly Metadata _metadata;

    public SpiceDbClient(SpiceDbSettings settings)
    {
        var channel = GrpcChannel.ForAddress(settings.Endpoint, new GrpcChannelOptions
        {
            Credentials = settings.UseTls ? ChannelCredentials.SecureSsl : ChannelCredentials.Insecure
        });

        _client = new PermissionsService.PermissionsServiceClient(channel);
        _metadata = new Metadata
        {
            { "authorization", $"Bearer {settings.PresharedKey}" }
        };
    }

    public async Task<bool> CheckPermissionAsync(
        string resourceType,
        string resourceId,
        string permission,
        string subjectType,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        var request = new CheckPermissionRequest
        {
            Resource = new ObjectReference
            {
                ObjectType = resourceType,
                ObjectId = resourceId
            },
            Permission = permission,
            Subject = new SubjectReference
            {
                Object = new ObjectReference
                {
                    ObjectType = subjectType,
                    ObjectId = subjectId
                }
            },
            Consistency = new Consistency { FullyConsistent = true }
        };

        var response = await _client.CheckPermissionAsync(
            request,
            headers: _metadata,
            cancellationToken: cancellationToken);

        return response.Permissionship == CheckPermissionResponse.Types.Permissionship.HasPermission;
    }

    public async Task WriteRelationshipAsync(
        string resourceType,
        string resourceId,
        string relation,
        string subjectType,
        string subjectId,
        string? caveatName = null,
        Dictionary<string, object>? caveatContext = null,
        CancellationToken cancellationToken = default)
    {
        var relationship = new Relationship
        {
            Resource = new ObjectReference
            {
                ObjectType = resourceType,
                ObjectId = resourceId
            },
            Relation = relation,
            Subject = new SubjectReference
            {
                Object = new ObjectReference
                {
                    ObjectType = subjectType,
                    ObjectId = subjectId
                }
            }
        };

        if (caveatName != null)
        {
            relationship.OptionalCaveat = new ContextualizedCaveat
            {
                CaveatName = caveatName
            };

            if (caveatContext != null)
            {
                var contextStruct = new Struct();
                foreach (var (key, value) in caveatContext)
                {
                    contextStruct.Fields[key] = value switch
                    {
                        string s => Value.ForString(s),
                        int i => Value.ForNumber(i),
                        long l => Value.ForNumber(l),
                        double d => Value.ForNumber(d),
                        bool b => Value.ForBool(b),
                        _ => Value.ForString(value.ToString()!)
                    };
                }
                relationship.OptionalCaveat.Context = contextStruct;
            }
        }

        var request = new WriteRelationshipsRequest
        {
            Updates =
            {
                new RelationshipUpdate
                {
                    Operation = RelationshipUpdate.Types.Operation.Touch,
                    Relationship = relationship
                }
            }
        };

        await _client.WriteRelationshipsAsync(
            request,
            headers: _metadata,
            cancellationToken: cancellationToken);
    }

    public async Task DeleteRelationshipAsync(
        string resourceType,
        string resourceId,
        string relation,
        string subjectType,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteRelationshipsRequest
        {
            RelationshipFilter = new RelationshipFilter
            {
                ResourceType = resourceType,
                OptionalResourceId = resourceId,
                OptionalRelation = relation,
                OptionalSubjectFilter = new SubjectFilter
                {
                    SubjectType = subjectType,
                    OptionalSubjectId = subjectId
                }
            }
        };

        await _client.DeleteRelationshipsAsync(
            request,
            headers: _metadata,
            cancellationToken: cancellationToken);
    }

    public async Task<List<string>> LookupResourcesAsync(
        string resourceType,
        string permission,
        string subjectType,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        var request = new LookupResourcesRequest
        {
            ResourceObjectType = resourceType,
            Permission = permission,
            Subject = new SubjectReference
            {
                Object = new ObjectReference
                {
                    ObjectType = subjectType,
                    ObjectId = subjectId
                }
            },
            Consistency = new Consistency { FullyConsistent = true }
        };

        var resources = new List<string>();
        var call = _client.LookupResources(request, headers: _metadata, cancellationToken: cancellationToken);

        await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
        {
            resources.Add(response.ResourceObjectId);
        }

        return resources;
    }
}
