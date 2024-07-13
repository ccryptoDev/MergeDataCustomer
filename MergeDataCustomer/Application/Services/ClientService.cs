using AutoMapper;
using MergeDataCustomer.Repositories.DtoModels.Requests;
using MergeDataCustomer.Repositories.DtoModels.Responses;
using MergeDataEntities.Schemas.Public;
using MergeDataImporter.Helpers.Generic;
using MergeDataImporter.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace MergeDataCustomer.Application.Services
{
    public class ClientService
    {
        private readonly RawContext _context;
        private readonly IMapper _mapper;

        public ClientService(RawContext context,
            IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<Result<List<ClientResponse>>> GetClients()
        {
            List<Client> clients = _context.Clients
                .Include(c => c.Stores)
                .Where(x => x.IsActive).ToList();

            var result = clients.Select(c => _mapper.Map<ClientResponse>(c)).ToList();

            return await Result<List<ClientResponse>>.SuccessAsync(result);
        }

        public async Task<Result<ClientResponse>> GetClient(long clientId)
        {
            Client client = _context.Clients
                .Include(c => c.Stores)
                .FirstOrDefault(x => x.ClientId == clientId);

            if (client == null)
            {
                return await Result<ClientResponse>.FailAsync("Client not found");
            }

            return await Result<ClientResponse>.SuccessAsync(_mapper.Map<ClientResponse>(client));
        }

        public async Task<Result<ClientResponse>> CreateClient(ClientRequest request)
        {
            var client = new Client
            {
                Name = request.Name,
                IsActive = true,
                StartDate = DateTime.UtcNow,
                LastUpdateDate = DateTime.UtcNow,
                RawFolderPath = string.Empty
            };

            await _context.Clients.AddAsync(client);

            await _context.SaveChangesAsync();

            return await Result<ClientResponse>.SuccessAsync(_mapper.Map<ClientResponse>(client));
        }

        public async Task<Result<ClientResponse>> UpdateClient(long clientId, ClientRequest request)
        {
            Client client = _context.Clients.FirstOrDefault(x => x.ClientId == clientId);

            if (client == null)
            {
                return await Result<ClientResponse>.FailAsync("Client not found");
            }

            client.Name = request.Name;
            client.LastUpdateDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await Result<ClientResponse>.SuccessAsync(_mapper.Map<ClientResponse>(client));
        }

        public async Task<Result<ClientResponse>> UpdateClientStatus(long clientId, bool isActive)
        {
            Client client = _context.Clients.FirstOrDefault(x => x.ClientId == clientId);

            if (client == null)
            {
                return await Result<ClientResponse>.FailAsync("Client not found");
            }

            client.IsActive = isActive;

            await _context.SaveChangesAsync();

            return await Result<ClientResponse>.SuccessAsync(_mapper.Map<ClientResponse>(client));
        }
    }
}
