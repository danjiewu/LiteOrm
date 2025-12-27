using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using MyOrm.Service;
using Microsoft.VSDiagnostics;

namespace MyOrm.Benchmarks
{
    public class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    // A lightweight derived service that overrides core methods to operate on an in-memory list
    public class InMemoryEntityService : EntityService<TestEntity>
    {
        private readonly List<TestEntity> _store = new List<TestEntity>();
        protected override bool InsertCore(TestEntity entity)
        {
            // simulate small work
            _store.Add(entity);
            return true;
        }

        protected override bool UpdateCore(TestEntity entity)
        {
            var idx = _store.FindIndex(e => e.Id == entity.Id);
            if (idx >= 0)
            {
                _store[idx] = entity;
                return true;
            }

            return false;
        }

        protected override bool DeleteCore(TestEntity entity)
        {
            return _store.RemoveAll(e => e.Id == entity.Id) > 0;
        }

        protected override bool DeleteIDCore(object id)
        {
            int intId = Convert.ToInt32(id);
            return _store.RemoveAll(e => e.Id == intId) > 0;
        }
    }

    [CPUUsageDiagnoser]
    public class EntityServiceBatchBenchmarks
    {
        private InMemoryEntityService _service;
        private List<EntityOperation<TestEntity>> _ops;
        private List<TestEntity> _entities;
        [GlobalSetup]
        public void Setup()
        {
            _service = new InMemoryEntityService();
            _entities = new List<TestEntity>();
            _ops = new List<EntityOperation<TestEntity>>();
            for (int i = 0; i < 1000; i++)
            {
                var e = new TestEntity
                {
                    Id = i,
                    Name = "Name" + i
                };
                _entities.Add(e);
                _ops.Add(new EntityOperation<TestEntity> { Operation = OpDef.Insert, Entity = e });
            }
        }

        [Benchmark]
        public void Batch_Insert_Sync()
        {
            _service.Batch(_ops);
        }

        [Benchmark]
        public void BatchInsert_Sync()
        {
            _service.BatchInsert(_entities);
        }

        [Benchmark]
        public void BatchUpdate_Sync()
        {
            // first ensure data exists
            _service.BatchInsert(_entities);
            _service.BatchUpdate(_entities);
        }

        [Benchmark]
        public void BatchDelete_Sync()
        {
            _service.BatchInsert(_entities);
            _service.BatchDelete(_entities);
        }

        [Benchmark]
        public Task Batch_Insert_Async()
        {
            return _service.BatchAsync(_ops, CancellationToken.None);
        }

        [Benchmark]
        public Task BatchInsert_Async()
        {
            return _service.BatchInsertAsync(_entities, CancellationToken.None);
        }

        [Benchmark]
        public Task BatchUpdate_Async()
        {
            _service.BatchInsert(_entities);
            return _service.BatchUpdateAsync(_entities, CancellationToken.None);
        }

        [Benchmark]
        public Task BatchDelete_Async()
        {
            _service.BatchInsert(_entities);
            return _service.BatchDeleteAsync(_entities, CancellationToken.None);
        }
    }
}