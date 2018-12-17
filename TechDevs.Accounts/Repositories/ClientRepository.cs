﻿using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using TechDevs.Accounts.Utils;

namespace TechDevs.Accounts.Repositories
{
    public interface IClientRepository
    {
        Task<List<Client>> GetClients();
        Task<Client> GetClient(string clientId, bool includeRelatedAuthUsers = false);
        Task<Client> CreateClient(Client client);
        Task<Client> UpdateClient<Type>(string propertyPath, Type data, string clientId);
        Task<Client> UpdateClient(string clientId, Client client);
        Task<Client> DeleteClient(string clientId);
        Task<Client> GetClientByShortKey(string shortKey);
    }

    public class ClientRepository : IClientRepository
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<AuthUser> _users;
        private readonly IMongoCollection<Client> _clients;

        public ClientRepository(IOptions<MongoDbSettings> dbSettings)
        {
            var client = new MongoClient(dbSettings.Value.ConnectionString);
            _database = client.GetDatabase(dbSettings.Value.Database);
            _clients = _database.GetCollection<Client>("Clients");
            _users = _database.GetCollection<AuthUser>("AuthUsers");
        }

        public async Task<List<Client>> GetClients()
        {
            var clients = await _clients.FindAsync(x => true);
            return await clients.ToListAsync();
        }

        public async Task<Client> GetClient(string clientId, bool includeRelatedAuthUsers = false)
        {
            var client = await _clients.Find(x => x.Id == clientId).FirstAsync();
            if (includeRelatedAuthUsers)
            {
                var clientFilter = new BsonDocument { { "ClientId", new BsonDocument { { "_id", client.Id } } } };

                var employees = await _users.OfType<Employee>().Find(clientFilter).ToListAsync();
                var customers = await _users.OfType<Customer>().Find(clientFilter).ToListAsync();

                client.Employees = employees.Select(e => new EmployeeProfile(e)).ToList();
                client.Customers = customers.Select(e => new CustomerProfile(e)).ToList();
            }
            return client;
        }

        public async Task<Client> CreateClient(Client client)
        {
            await _clients.InsertOneAsync(client);
            var result = await GetClient(client.Id);
            return result;
        }

        public async Task<Client> DeleteClient(string clientId)
        {
            var result = await _clients.FindOneAndDeleteAsync(x => x.Id == clientId);
            return result;
        }

        public async Task<Client> UpdateClient<Type>(string propertyPath, Type data, string clientId)
        {
            propertyPath = propertyPath.FirstLetterUpper();

            FilterDefinition<Client> filter = Builders<Client>.Filter.Eq(x => x.Id, clientId);
            UpdateDefinition<Client> update = Builders<Client>.Update.Set(propertyPath, data);

            var result = await _clients.UpdateOneAsync(filter, update);
            if (result.IsAcknowledged) return await GetClient(clientId);
            throw new Exception("Client could not be updated");
        }

        public async Task<Client> UpdateClient(string clientId, Client client)
        {
            FilterDefinition<Client> filter = Builders<Client>.Filter.Eq(x => x.Id, clientId);
            var updateOptions = new UpdateOptions {IsUpsert = false};
            var result = await _clients.ReplaceOneAsync(filter, client, updateOptions);
            if (!result.IsAcknowledged || result.ModifiedCount == 0 ) throw new Exception("Client could not be updated");
            return await GetClient(clientId, false);
        }

        public async Task<Client> GetClientByShortKey(string shortKey)
        {
            var client = await _clients.Find(x => x.ShortKey == shortKey).FirstOrDefaultAsync();
            return client;
        }
    }
}
