#region LICENSE
// Copyright (c) Alexey Malinin. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
#endregion


using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Camunda.Worker.Execution
{
    public class StaticTopicsProviderTest
    {
        [Fact]
        public void TestGetTopics()
        {
            var descriptors = GetDescriptors().ToList();

            var topicsProvider = new StaticTopicsProvider(descriptors);

            var topics = topicsProvider.GetTopics().ToList();

            Assert.Equal(2, topics.Count);

            Assert.Equal(descriptors[0].TopicName, topics[0].TopicName);
            Assert.Null(topics[0].Variables);

            Assert.Equal(descriptors[1].TopicName, topics[1].TopicName);
            Assert.NotNull(topics[1].Variables);
            Assert.True(topics[1].LocalVariables);
            Assert.Equal(descriptors[1].LockDuration, topics[1].LockDuration);
        }

        private static IEnumerable<HandlerDescriptor> GetDescriptors()
        {
            IExternalTaskHandler Factory(IServiceProvider provider) => null;
            var descriptors = new[]
            {
                new HandlerDescriptor("test1", Factory),
                new HandlerDescriptor("test2", Factory)
                {
                    Variables = new[] {"X"},
                    LockDuration = 10_000,
                    LocalVariables = true
                }
            };
            return descriptors;
        }
    }
}
