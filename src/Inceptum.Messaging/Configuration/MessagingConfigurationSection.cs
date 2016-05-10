﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Configuration
{
    public class MessagingConfigurationSection : ConfigurationSection, IMessagingConfiguration
    {
        [ConfigurationProperty("transports", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof (TransportsCollection),
            AddItemName = "add",
            ClearItemsName = "clear",
            RemoveItemName = "remove")]
        public TransportsCollection Transports
        {
            get { return (TransportsCollection) base["transports"]; }
        }

        [ConfigurationProperty("endpoints", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof (EndpointsCollection),
            AddItemName = "add",
            ClearItemsName = "clear",
            RemoveItemName = "remove")]
        public EndpointsCollection Endpoints
        {
            get { return (EndpointsCollection) base["endpoints"]; }
        }

        [ConfigurationProperty("processingGroups", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof (ProcessingGroupsCollection),
            AddItemName = "add",
            ClearItemsName = "clear",
            RemoveItemName = "remove")]
        public ProcessingGroupsCollection ProcessingGroups
        {
            get { return (ProcessingGroupsCollection)base["processingGroups"]; }
        }

        public IDictionary<string, TransportInfo> GetTransports()
        {
            return Transports.Cast<TransportConfigurationElement>()
                             .ToDictionary(tce => tce.Name,
                                           tce =>
                                           new TransportInfo(tce.Broker, tce.Login, tce.Password, tce.JailStrategyName,
                                                             tce.Messaging));
        }

        public IDictionary<string, Endpoint> GetEndpoints()
        {
            return Endpoints.Cast<EndpointConfigurationElement>().ToDictionary(ece => ece.Name, ece => ece.ToEndpoint());
        }

        public IDictionary<string, ProcessingGroupInfo> GetProcessingGroups()
        {
            return ProcessingGroups.Cast<ProcessingGroupConfigurationElement>().ToDictionary(pge => pge.Name, pge => new ProcessingGroupInfo() { ConcurrencyLevel = pge.ConcurrencyLevel,QueueCapacity = pge.QueueCapacity});
        }
    }
}