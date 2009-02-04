using System;
using System.Collections.Generic;
using com.mosso.cloudfiles.domain;
using com.mosso.cloudfiles.domain.request;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;

namespace com.mosso.cloudfiles.integration.tests.Domain.CF.ContainerSpecs
{
    [TestFixture]
    public class ContainerIntegrationTestBase
    {
        protected string containerName;
        protected IAccount account;
        protected IContainer container;

        [SetUp]
        public void Setup()
        {
            containerName = Guid.NewGuid().ToString();

            var userCredentials = new UserCredentials(Constants.MOSSO_USERNAME, Constants.MOSSO_API_KEY);
            var authentication = new CF_Authentication(userCredentials);

            account = authentication.Authenticate();
            container = account.CreateContainer(containerName);
        }

        [TearDown]
        public void TearDown()
        {
            if (container.ObjectExists(Constants.StorageItemName))
                container.DeleteObject(Constants.StorageItemName);

            if (container.ObjectExists(Constants.HeadStorageItemName))
                container.DeleteObject(Constants.HeadStorageItemName);

            if (containerName != null && container != null)
                account.DeleteContainer(containerName);
        } 
    }

    [TestFixture]
    public class When_making_a_container_public : ContainerIntegrationTestBase
    {
        [Test]
        public void Should_obtain_a_public_url()
        {
            container.MarkAsPublic();

            Assert.That(container.PublicUrl.ToString().Contains("http://cdn.cloudfiles.mosso.com/"), Is.True);
        }
    }

    [TestFixture]
    public class When_adding_an_object_to_the_container_via_file_path_successfully_without_metadata : ContainerIntegrationTestBase
    {
        [Test]
        public void should_add_the_object()
        {
            IObject @object = container.AddObject(Constants.StorageItemName);

            Assert.That(@object.Name, Is.EqualTo(Constants.StorageItemName));
            Assert.That(container.ObjectExists(Constants.StorageItemName), Is.True);
            Assert.That(container.ObjectCount, Is.EqualTo(1));
            Assert.That(container.BytesUsed, Is.EqualTo(34));
        }
    }

    [TestFixture]
    public class When_adding_an_object_to_the_container_via_file_path_successfully_with_metadata : ContainerIntegrationTestBase
    {
        [Test]
        public void should_add_the_object()
        {
            Dictionary<string, string> metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");
            metadata.Add("key3", "value3");
            metadata.Add("key4", "value4");
            metadata.Add("key5", "value5");
            IObject @object = container.AddObject(Constants.StorageItemName, metadata);

            Assert.That(@object.Name, Is.EqualTo(Constants.StorageItemName));
            Assert.That(container.ObjectExists(Constants.StorageItemName), Is.True);
        }

        [Test]
        public void should_give_object_count_and_bytes_used()
        {
            Dictionary<string, string> metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");
            metadata.Add("key3", "value3");
            metadata.Add("key4", "value4");
            metadata.Add("key5", "value5");
            IObject @object = container.AddObject(Constants.StorageItemName, metadata);

            Assert.That(@object.Name, Is.EqualTo(Constants.StorageItemName));
            Assert.That(container.ObjectExists(Constants.StorageItemName), Is.True);

            Assert.That(container.ObjectCount, Is.EqualTo(1));
            Assert.That(container.BytesUsed, Is.EqualTo(34));
        }
    }

    [TestFixture]
    public class When_getting_an_object_list_from_the_container_with_the_limit_query_parameter : ContainerIntegrationTestBase
    {
        [Test]
        public void should_return_only_the_specified_number_of_objects()
        {
            container.AddObject(Constants.StorageItemName);
            Assert.That(container.ObjectExists(Constants.StorageItemName), Is.True);
            container.AddObject(Constants.HeadStorageItemName);
            Assert.That(container.ObjectExists(Constants.HeadStorageItemName), Is.True);

            string[] objectNames = container.GetObjectNames();
            Assert.That(objectNames.Length, Is.EqualTo(2));
            Assert.That(objectNames[0], Is.EqualTo(Constants.HeadStorageItemName));
            Assert.That(objectNames[1], Is.EqualTo(Constants.StorageItemName));

            Dictionary<GetItemListParameters, string> parameters = new Dictionary<GetItemListParameters, string>();
            parameters.Add(GetItemListParameters.Limit, "1");
            objectNames = container.GetObjectNames(parameters);
            Assert.That(objectNames.Length, Is.EqualTo(1));
            Assert.That(objectNames[0], Is.EqualTo(Constants.HeadStorageItemName));
        }
    }

    [TestFixture]
    public class When_getting_an_object_list_from_the_container_with_the_offset_query_parameter : ContainerIntegrationTestBase
    {
        [Test]
        public void should_return_only_objects_starting_from_the_offset()
        {
            container.AddObject(Constants.StorageItemName);
            Assert.That(container.ObjectExists(Constants.StorageItemName), Is.True);
            container.AddObject(Constants.HeadStorageItemName);
            Assert.That(container.ObjectExists(Constants.HeadStorageItemName), Is.True);

            string[] objectNames = container.GetObjectNames();
            Assert.That(objectNames.Length, Is.EqualTo(2));
            Assert.That(objectNames[0], Is.EqualTo(Constants.HeadStorageItemName));
            Assert.That(objectNames[1], Is.EqualTo(Constants.StorageItemName));

            Dictionary<GetItemListParameters, string> parameters = new Dictionary<GetItemListParameters, string>();
            parameters.Add(GetItemListParameters.Offset, "1");
            objectNames = container.GetObjectNames(parameters);
            Assert.That(objectNames.Length, Is.EqualTo(1));
            Assert.That(objectNames[0], Is.EqualTo(Constants.StorageItemName));
        }
    }

    [TestFixture]
    public class When_getting_an_object_list_from_the_container_with_the_prefix_query_parameter : ContainerIntegrationTestBase
    {
        [Test]
        public void should_return_only_objects_beginning_with_the_provided_substring()
        {
            container.AddObject(Constants.StorageItemName);
            Assert.That(container.ObjectExists(Constants.StorageItemName), Is.True);
            container.AddObject(Constants.HeadStorageItemName);
            Assert.That(container.ObjectExists(Constants.HeadStorageItemName), Is.True);

            string[] objectNames = container.GetObjectNames();
            Assert.That(objectNames.Length, Is.EqualTo(2));
            Assert.That(objectNames[0], Is.EqualTo(Constants.HeadStorageItemName));
            Assert.That(objectNames[1], Is.EqualTo(Constants.StorageItemName));

            Dictionary<GetItemListParameters, string> parameters = new Dictionary<GetItemListParameters, string>();
            parameters.Add(GetItemListParameters.Prefix, "H");
            objectNames = container.GetObjectNames(parameters);
            Assert.That(objectNames.Length, Is.EqualTo(1));
            Assert.That(objectNames[0], Is.EqualTo(Constants.HeadStorageItemName));

            parameters.Clear();
            parameters.Add(GetItemListParameters.Prefix, "T");
            objectNames = container.GetObjectNames(parameters);
            Assert.That(objectNames.Length, Is.EqualTo(1));
            Assert.That(objectNames[0], Is.EqualTo(Constants.StorageItemName));
        }
    }
}