// -----------------------------------------------------------------------
// <copyright file="PartitionManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Linq;
using Proto.Cluster.Partition;

namespace Proto.Cluster.PartitionActivator
{
    //helper to interact with partition actors on this and other members
    public class PartitionActivatorManager
    {
        private const string PartitionActivatorActorName = "partition-activator";
        private readonly Cluster _cluster;


        private readonly IRootContext _context;
        private readonly bool _isClient;
        private readonly ActorSystem _system;
        private PID _partitionActivatorActor = null!;

        internal PartitionActivatorManager(Cluster cluster, bool isClient)
        {
            _cluster = cluster;
            _system = cluster.System;
            _context = _system.Root;
            _isClient = isClient;
 
        }

        internal PartitionActivatorSelector Selector { get; } = new();

        public void Setup()
        {
            if (_isClient)
            {
                var topologyHash = 0ul;
                //make sure selector is updated first
                _system.EventStream.Subscribe<ClusterTopology>(e => {
                        if (e.TopologyHash == topologyHash) return;

                        topologyHash = e.TopologyHash;
                        Selector.Update(e.Members.ToArray(),topologyHash);
                    }
                );
            }
            else
            {
                var partitionActivatorProps =
                    Props.FromProducer(() => new PartitionActivatorActor(_cluster, this));
                _partitionActivatorActor = _context.SpawnNamed(partitionActivatorProps, PartitionActivatorActorName);

                //synchronous subscribe to keep accurate

                var topologyHash = 0ul;
                //make sure selector is updated first
                _system.EventStream.Subscribe<ClusterTopology>(e => {
                        if (e.TopologyHash == topologyHash) return;

                        topologyHash = e.TopologyHash;

                        Selector.Update(e.Members.ToArray(),topologyHash);
                        _context.Send(_partitionActivatorActor, e);
                    }
                );
            }
        }

        public void Shutdown()
        {
            if (_isClient)
            {
            }
            else _context.Stop(_partitionActivatorActor);
        }

        public static PID RemotePartitionActivatorActor(string address) =>
            PID.FromAddress(address, PartitionActivatorActorName);
    }
}