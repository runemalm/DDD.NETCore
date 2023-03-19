﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DDD.Infrastructure.Ports.Adapters.Common.Translation.Converters;

namespace DDD.Infrastructure.Services.Persistence.Memory
{
    public class MemoryConnection : Connection
    {
        private bool _hasConnection;
        private bool _hasTransaction;

        public MemoryConnection(SerializerSettings serializerSettings) : base("n/a", serializerSettings)
        {
            
        }

        public override async Task OpenAsync()
        {
            _hasConnection = true;
        }
        
        public override async Task CloseAsync()
        {
            _hasConnection = false;
        }

        public override Task StartTransactionAsync()
        {
            if (!_hasConnection)
                throw new ApplicationException(
                    "Can't start transaction, no connection has been made.");
            _hasTransaction = true;
            return Task.CompletedTask;
        }
        
        public override async Task CommitTransactionAsync()
        {
            if (!_hasTransaction)
                throw new ApplicationException(
                    "Can't commit non-existing transaction.");
            _hasTransaction = false;
        }
        
        public override async Task RollbackTransactionAsync()
        {
            if (!_hasTransaction)
                throw new ApplicationException(
                    "Can't rollback non-existing transaction.");
            _hasTransaction = false;
        }
        
        public override async Task<int> ExecuteNonQueryAsync(string stmt, IDictionary<string, object> parameters)
        {
            return await Task.FromResult(0);
        }

        public override async Task<IEnumerable<T>> ExecuteQueryAsync<T>(string stmt, IDictionary<string, object> parameters)
        {
            return await Task.FromResult(new List<T>());
        }
        
        public override async Task<T> ExecuteScalarAsync<T>(string stmt, IDictionary<string, object>? parameters)
        {
            throw new NotImplementedException();
        }
    }
}
