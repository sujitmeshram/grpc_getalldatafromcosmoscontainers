using Grpc.Core;
using Microsoft.Azure.Cosmos;

namespace grpcCosmos.Services
{
    public class CosmosServiceImpl:CosmosService.CosmosServiceBase
    {
        public override async Task<ContainersResponse> GetContainersWithDataAndSchema(CosmosCredentials request, ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(request.AccountEndpoint) ||
                string.IsNullOrWhiteSpace(request.AccountKey) ||
                string.IsNullOrWhiteSpace(request.DatabaseName))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid credentials."));
            }

            // Create a CosmosClient instance using credentials from the request
            var cosmosClient = new CosmosClient(request.AccountEndpoint, request.AccountKey);

            var containersData = await GetContainersWithDataAsync(cosmosClient, request.DatabaseName);

            var response = new ContainersResponse();

            foreach (var container in containersData)
            {
                var containerData = new ContainerData();

                foreach (var item in container.Value)
                {
                    var dataItem = new DataItem();
                    foreach (var keyValue in item)
                    {
                        dataItem.Fields.Add(keyValue.Key, keyValue.Value?.ToString() ?? "null");
                    }
                    containerData.Data.Add(dataItem);
                }

                response.ContainersData.Add(container.Key, containerData);
            }

            return response;
        }

        private async Task<Dictionary<string, List<Dictionary<string, object>>>> GetContainersWithDataAsync(CosmosClient cosmosClient, string databaseName)
        {
            var containersData = new Dictionary<string, List<Dictionary<string, object>>>();
            var containerNames = await GetContainersAsync(cosmosClient, databaseName);

            foreach (var containerName in containerNames)
            {
                var container = cosmosClient.GetContainer(databaseName, containerName);
                var items = new List<Dictionary<string, object>>();

                var query = new QueryDefinition("SELECT * FROM c");
                var iterator = container.GetItemQueryIterator<Dictionary<string, object>>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    items.AddRange(response.ToList());
                }

                containersData.Add(containerName, items);
            }

            return containersData;
        }

        private async Task<List<string>> GetContainersAsync(CosmosClient cosmosClient, string databaseName)
        {
            var database = cosmosClient.GetDatabase(databaseName);
            var containerIterator = database.GetContainerQueryIterator<ContainerProperties>();
            var containers = new List<string>();

            while (containerIterator.HasMoreResults)
            {
                var response = await containerIterator.ReadNextAsync();
                containers.AddRange(response.Select(c => c.Id));
            }

            return containers;
        }
    }
}
