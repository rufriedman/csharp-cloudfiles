///
/// See COPYING file for licensing information
///

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml;
using Rackspace.CloudFiles.Domain;
using Rackspace.CloudFiles.Domain.Request;
using Rackspace.CloudFiles.Domain.Response;
using Rackspace.CloudFiles.Exceptions;
using Rackspace.CloudFiles.Utils;

/// <example>
/// <code>
/// UserCredentials userCredentials = new UserCredentials("username", "api key");
/// IConnection connection = new Connection(userCredentials);
/// </code>
/// </example>
namespace Rackspace.CloudFiles
{
    /// <summary>
    /// enumeration of filters to place on the request url
    /// </summary>
    public enum GetListParameters
    {
        Limit,
        Marker,
        Prefix,
        Path
    }

    /// <summary>
    /// This class represents the primary means of interaction between a user and cloudfiles. Methods are provided representing all of the actions
    /// one can take against his/her account, such as creating containers and downloading storage objects. 
    /// </summary>
    /// <example>
    /// <code>
    /// UserCredentials userCredentials = new UserCredentials("username", "api key");
    /// IConnection connection = new Connection(userCredentials);
    /// </code>
    /// </example>
    public class Connection : IConnection
    {
        private bool _retry;
        private readonly List<ProgressCallback> _callbackFuncs;
        private readonly GenerateRequestByType _requestfactory;
        private readonly bool _useServiceNet;
        protected string CdnManagementUrl;
        protected UserCredentials _usercreds;

        /// <summary>
        /// A constructor used to create an instance of the Connection class
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// </code>
        /// </example>
        /// <param name="userCreds">An instance of the UserCredentials class, containing all pertinent authentication information</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public Connection(UserCredentials userCreds)
            : this(userCreds, false)
        {
        }

        public Connection(UserCredentials userCreds, bool useServiceNet)
        {
            _useServiceNet = useServiceNet;
            _requestfactory = new GenerateRequestByType();
            _callbackFuncs = new List<ProgressCallback>();
            Log.EnsureInitialized();

            if (userCreds == null) throw new ArgumentNullException("userCreds");

            _usercreds = userCreds;

            VerifyAuthentication();
        }

        protected virtual void VerifyAuthentication()
        {
            if (!IsAuthenticated())
            {
                Authenticate();
            }
        }

        public Action<Exception> Nothing = ex => { };

        public delegate void OperationCompleteCallback();

        public event OperationCompleteCallback OperationComplete;

        public delegate void ProgressCallback(int bytesWritten);

        public IAccount Account
        {
            get
            {
                if (IsAuthenticated())
                    return new CF_Account(this);

                Authenticate();

                return null;
            }
        }

        public string StorageUrl { get; private set; }

        public string AuthToken { private set; get; }

        public Boolean HasCDN()
        {
            return !string.IsNullOrEmpty(CdnManagementUrl);
        }

        public void AddProgressWatcher(ProgressCallback progressCallback)
        {
            _callbackFuncs.Add(progressCallback);
        }

        /// <summary>
        /// This method returns the number of containers and the size, in bytes, of the specified account
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// AccountInformation accountInformation = connection.GetAccountInformation();
        /// </code>
        /// </example>
        /// <returns>An instance of AccountInformation, containing the byte size and number of containers associated with this account</returns>
        public AccountInformation GetAccountInformation()
        {
            return StartProcess.
                ByLoggingMessage("Getting account information for user " + _usercreds.Username)
                .ThenDoing<AccountInformation>(BuildAccount).
                AndIfErrorThrownIs<Exception>().
                Do(Nothing).
                AndLogError("Error getting account information for user " + _usercreds.Username).
                Now();
        }

        /// <summary>
        /// Get account information in json format
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// string jsonReturnValue = connection.GetAccountInformationJson();
        /// </code>
        /// </example>
        /// <returns>JSON serialized format of the account information</returns>
        public string GetAccountInformationJson()
        {
            return StartProcess
                .ByLoggingMessage("Getting account information (JSON format) for user " + _usercreds.Username)
                .ThenDoing<string>(BuildAccountJson)
                .AndIfErrorThrownIs<Exception>()
                .Do(Nothing)
                .AndLogError("Error getting account information (JSON format) for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// Get account information in xml format
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// XmlDocument xmlReturnValue = connection.GetAccountInformationXml();
        /// </code>
        /// </example>
        /// <returns>XML serialized format of the account information</returns>
        public XmlDocument GetAccountInformationXml()
        {
            return StartProcess
                .ByLoggingMessage("Getting account information (XML format) for user " + _usercreds.Username)
                .ThenDoing<XmlDocument>(BuildAccountXml)
                .AndIfErrorThrownIs<Exception>()
                .Do(Nothing)
                .AndLogError("Error getting account information (XML format) for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method is used to create a container on cloudfiles with a given name
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.CreateContainer("container name");
        /// </code>
        /// </example>
        /// <param name="containerName">The desired name of the container</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void CreateContainer(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Creating container '" + containerName + "' for user " + _usercreds.Username)
                .ThenDoing(() => ContainerCreation(containerName))
                .AndIfErrorThrownIs<Exception>()
                .Do(Nothing)
                .AndLogError("Error creating container '" + containerName + "' for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method is used to delete a container on cloudfiles
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.DeleteContainer("container name");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to delete</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void DeleteContainer(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Deleting container '" + containerName + "' for user " + _usercreds.Username)
                .ThenDoing(() => deleteContainer(StorageUrl, containerName))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error deleting container '" + containerName + "' for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method is used to delete a public container on cloudfiles
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.PurgePublicContainer("container name");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the public container to purge</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PurgePublicContainer(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Purging public container '" + containerName + "' for user " + _usercreds.Username)
                .ThenDoing(() => purgePublicContainer(CdnManagementUrl, containerName,null))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error purging public container '" + containerName + "' for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method is used to delete a public container on cloudfiles
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.PurgePublicContainer("container name", "me@me.com);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to delete</param>
        /// <param name="emailAddresses">The email addresses to notify once the deletion is complete</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PurgePublicContainer(string containerName, string[] emailAddresses)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Purging public container '" + containerName + "' for user " + _usercreds.Username)
                .ThenDoing(() => purgePublicContainer(CdnManagementUrl, containerName, emailAddresses))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error purging public container '" + containerName + "' for user " + _usercreds.Username)
                .Now();
        }


        /// <summary>
        /// This method retrieves a list of containers associated with a given account
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// List{string} containers = connection.GetContainers();
        /// </code>
        /// </example>
        /// <returns>An instance of List, containing the names of the containers this account owns</returns>
        public List<string> GetContainers()
        {
            return StartProcess
                .ByLoggingMessage("Getting containers for user " + _usercreds.Username)
                .ThenDoing<List<string>>(BuildContainerList)
                .AndIfErrorThrownIs<Exception>()
                .Do(Nothing)
                .AndLogError("Error getting containers for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method retrieves a list of containers associated with a given account
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Dictionary{GetListParameters, string} parameters = new Dictionary{GetListParameters, string}();
        /// parameters.Add(GetListParameters.Limit, 2);
        /// parameters.Add(GetListParameters.Marker, 1);
        /// parameters.Add(GetListParameters.Prefix, "a");
        /// List{string} containers = connection.GetContainers();
        /// </code>
        /// </example>
        /// <param name="parameters">Parameters to feed to the request to filter the returned list</param>
        /// <returns>An instance of List, containing the names of the containers this account owns</returns>
        public List<string> GetContainers(Dictionary<GetListParameters, string> parameters)
        {
            return StartProcess
                .ByLoggingMessage("Getting containers for user " + _usercreds.Username)
                .ThenDoing(() => BuildContainerList(parameters))
                .AndIfErrorThrownIs<Exception>()
                .Do(Nothing)
                .AndLogError("Error getting containers for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method retrieves the contents of a container
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// List{string} containerItemList = connection.GetContainerItemList("container name");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container</param>
        /// <returns>An instance of List, containing the names of the storage objects in the give container</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public List<string> GetContainerItemList(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            return StartProcess
                .ByLoggingMessage("Getting container item list for container '" + containerName + "' for user " + _usercreds.Username)
                .ThenDoing(() => GetContainerItemList(containerName, null))
                .AndIfErrorThrownIs<Exception>()
                .Do(Nothing)
                .AndLogError("Error getting item list for container '" + containerName + "' for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method ensures directory objects created for the entire path
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.MakePath("containername", "/dir1/dir2/dir3/dir4");
        /// </code>
        /// </example>
        /// <param name="containerName">The container to create the directory objects in</param>
        /// <param name="path">The path of directory objects to create</param>
        public void MakePath(string containerName, string path)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(path))
                throw new ArgumentNullException();

            StartProcess
               .ByLoggingMessage("Make path " + path + " for container '" + containerName + "' for user " + _usercreds.Username)
               .ThenDoing(() => makePath(containerName, path))
               .AndIfErrorThrownIs<Exception>()
               .Do(Nothing)
               .AndLogError("Error making path "+ path + " in container '"+ containerName + "' for user "+ _usercreds.Username)
               .Now();
        }


        /// <summary>
        /// This method retrieves the contents of a container
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Dictionary{GetListParameters, string} parameters = new Dictionary{GetListParameters, string}();
        /// parameters.Add(GetListParameters.Limit, 2);
        /// parameters.Add(GetListParameters.Marker, 1);
        /// parameters.Add(GetListParameters.Prefix, "a");
        /// List{string} containerItemList = connection.GetContainerItemList("container name", parameters);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container</param>
        /// <param name="parameters">Parameters to feed to the request to filter the returned list</param>
        /// <returns>An instance of List, containing the names of the storage objects in the give container</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public List<string> GetContainerItemList(string containerName, Dictionary<GetListParameters, string> parameters)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            return StartProcess
                .ByLoggingMessage("Getting container item list for container '"+ containerName + "' for user "+ _usercreds.Username)
                .ThenDoing(() => getContainerItemList(containerName, parameters))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error getting containers item list for container '"+ containerName + "' for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method retrieves the number of storage objects in a container, and the total size, in bytes, of the container
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Container container = connection.GetContainerInformation("container name");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to query about</param>
        /// <returns>An instance of container, with the number of storage objects contained and total byte allocation</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public Container GetContainerInformation(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            return StartProcess
                .ByLoggingMessage("Getting container information for container '"+ containerName + "' for user "+ _usercreds.Username)
                .ThenDoing(() => getContainerInformation(containerName))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error getting container information for container '"+ containerName + "' for user "+ _usercreds.Username)
                .Now();
        }


        /// <summary>
        /// JSON serialized format of the container's objects
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// string jsonResponse = connection.GetContainerInformationJson("container name");
        /// </code>
        /// </example>
        /// <param name="containerName">name of the container to get information</param>
        /// <returns>json string of object information inside the container</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public string GetContainerInformationJson(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            return StartProcess
                .ByLoggingMessage("Getting container information (JSON format) for container '"+ containerName + "' for user "+ _usercreds.Username)
                .ThenDoing(() => getContainerInformationJson(containerName))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error getting container information (JSON format) for container '"+ containerName + "' for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// XML serialized format of the container's objects
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// XmlDocument xmlResponse = connection.GetContainerInformationXml("container name");
        /// </code>
        /// </example>
        /// <param name="containerName">name of the container to get information</param>
        /// <returns>xml document of object information inside the container</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public XmlDocument GetContainerInformationXml(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            return StartProcess
                .ByLoggingMessage("Getting container information (XML format) for container '"+ containerName + "' for user "+ _usercreds.Username)
                .ThenDoing(() => getContainerInformationXml(containerName))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error getting container information (XML format) for container '"+ containerName + "' for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method uploads a storage object to cloudfiles with meta tags
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Dictionary{string, string} metadata = new Dictionary{string, string}();
        /// metadata.Add("key1", "value1");
        /// metadata.Add("key2", "value2");
        /// metadata.Add("key3", "value3");
        /// connection.PutStorageItem("container name", "C:\Local\File\Path\file.txt", metadata);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to put the storage object in</param>
        /// <param name="localFilePath">The complete file path of the storage object to be uploaded</param>
        /// <param name="metadata">An optional parameter containing a dictionary of meta tags to associate with the storage object</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PutStorageItem(string containerName, string localFilePath, Dictionary<string, string> metadata)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(localFilePath))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Putting storage item into container '"+ containerName + "' for user "+ _usercreds.Username)
                .ThenDoing(() => putStorageItem(containerName, localFilePath, metadata))
                .AndIfErrorThrownIs<WebException>()
                .Do(ex => DetermineReasonForStorageItemError(ex, true))
                .AndLogError("Error putting storage item "+ localFilePath + " with metadata into container '"+ containerName + "' for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method uploads a storage object to cloudfiles
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.PutStorageItem("container name", "C:\Local\File\Path\file.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to put the storage object in</param>
        /// <param name="localFilePath">The complete file path of the storage object to be uploaded</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PutStorageItem(string containerName, string localFilePath)
        {
            PutStorageItem(containerName, localFilePath, new Dictionary<string, string>());
        }

        /// <summary>
        /// This method uploads a storage object to cloudfiles with an alternate name
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// FileInfo file = new FileInfo("C:\Local\File\Path\file.txt");
        /// connection.PutStorageItem("container name", file.Open(FileMode.Open), "RemoteFileName.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to put the storage object in</param>
        /// <param name="remoteStorageItemName">The alternate name as it will be called on cloudfiles</param>
        /// <param name="storageStream">The stream representing the storage item to upload</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PutStorageItem(string containerName, Stream storageStream, string remoteStorageItemName)
        {
            PutStorageItem(containerName, storageStream, remoteStorageItemName, new Dictionary<string, string>());
        }

        /// <summary>
        /// This method uploads a storage object to cloudfiles asychronously
        /// </summary>
        /// <example>
        /// <code>
        /// private void transferComplete()
        /// {
        ///     if (InvokeRequired)
        ///     {
        ///         Invoke(new CloseCallback(Close), new object[]{});
        ///     }
        ///     else
        ///     {
        ///         if (!IsDisposed)
        ///             Close();
        ///     }
        /// }
        /// 
        /// private void fileTransferProgress(int bytesTransferred)
        /// {
        ///    if (InvokeRequired)
        ///    {
        ///        Invoke(new FileProgressCallback(fileTransferProgress), new object[] {bytesTransferred});
        ///    }
        ///    else
        ///    {
        ///        System.Console.WriteLine(totalTransferred.ToString());
        ///        totalTransferred += bytesTransferred;
        ///        bytesTransferredLabel.Text = totalTransferred.ToString();
        ///        var progress = (int) ((totalTransferred/filesize)*100.0f);
        ///        if(progress > 100)
        ///            progress = 100;
        ///        transferProgressBar.Value = progress ;
        ///    }
        /// }
        /// 
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.AddProgressWatcher(fileTransferProgress);
        /// connection.OperationComplete += transferComplete;
        /// connection.PutStorageItemAsync("container name", "RemoteStorageItem.txt", "RemoteStorageItem.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to put the storage object in</param>
        /// <param name="remoteStorageItemName">The alternate name as it will be called on cloudfiles</param>
        /// <param name="storageStream">The stream representing the storage item to upload</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PutStorageItemAsync(string containerName, Stream storageStream, string remoteStorageItemName)
        {
            var thread = new Thread(
                () =>
                    {
                        try
                        {
                            PutStorageItem(containerName, storageStream, remoteStorageItemName);
                        }
                        finally //Always fire the completed event
                        {
                            if (OperationComplete != null)
                            {
                                //Fire the operation complete event if there are any listeners
                                OperationComplete();
                            }
                        }
                    }
                );
            thread.Start();
        }

        /// <summary>
        /// This method uploads a storage object to cloudfiles asychronously
        /// </summary>
        /// <example>
        /// <code>
        /// private void transferComplete()
        /// {
        ///     if (InvokeRequired)
        ///     {
        ///         Invoke(new CloseCallback(Close), new object[]{});
        ///     }
        ///     else
        ///     {
        ///         if (!IsDisposed)
        ///             Close();
        ///     }
        /// }
        /// 
        /// private void fileTransferProgress(int bytesTransferred)
        /// {
        ///    if (InvokeRequired)
        ///    {
        ///        Invoke(new FileProgressCallback(fileTransferProgress), new object[] {bytesTransferred});
        ///    }
        ///    else
        ///    {
        ///        System.Console.WriteLine(totalTransferred.ToString());
        ///        totalTransferred += bytesTransferred;
        ///        bytesTransferredLabel.Text = totalTransferred.ToString();
        ///        var progress = (int) ((totalTransferred/filesize)*100.0f);
        ///        if(progress > 100)
        ///            progress = 100;
        ///        transferProgressBar.Value = progress ;
        ///    }
        /// }
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Dictionary{string, string} metadata = new Dictionary{string, string}();
        /// metadata.Add("key1", "value1");
        /// metadata.Add("key2", "value2");
        /// metadata.Add("key3", "value3");
        /// connection.PutStorageItemAsync("container name", "LocalFileName.txt", metadata);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to put the storage object in</param>
        /// <param name="localStorageItemName">The name of the file locally </param>
        /// <param name="metadata">An optional parameter containing a dictionary of meta tags to associate with the storage object</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PutStorageItemAsync(string containerName, string localStorageItemName, Dictionary<string, string> metadata)
        {
            var thread = new Thread(
                () =>
                    {
                        try
                        {
                            PutStorageItem(containerName, localStorageItemName, metadata);
                        }
                        finally //Always fire the completed event
                        {
                            if (OperationComplete != null)
                            {
                                //Fire the operation complete event if there aren't any listeners
                                OperationComplete();
                            }
                        }
                    }
                );
            thread.Start();
        }

        /// <summary>
        /// This method uploads a storage object to cloudfiles asychronously
        /// </summary>
        /// <example>
        /// <code>
        /// private void transferComplete()
        /// {
        ///     if (InvokeRequired)
        ///     {
        ///         Invoke(new CloseCallback(Close), new object[]{});
        ///     }
        ///     else
        ///     {
        ///         if (!IsDisposed)
        ///             Close();
        ///     }
        /// }
        /// 
        /// private void fileTransferProgress(int bytesTransferred)
        /// {
        ///    if (InvokeRequired)
        ///    {
        ///        Invoke(new FileProgressCallback(fileTransferProgress), new object[] {bytesTransferred});
        ///    }
        ///    else
        ///    {
        ///        System.Console.WriteLine(totalTransferred.ToString());
        ///        totalTransferred += bytesTransferred;
        ///        bytesTransferredLabel.Text = totalTransferred.ToString();
        ///        var progress = (int) ((totalTransferred/filesize)*100.0f);
        ///        if(progress > 100)
        ///            progress = 100;
        ///        transferProgressBar.Value = progress ;
        ///    }
        /// }
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Dictionary{string, string} metadata = new Dictionary{string, string}();
        /// metadata.Add("key1", "value1");
        /// metadata.Add("key2", "value2");
        /// metadata.Add("key3", "value3");
        /// FileInfo file = new FileInfo("C:\Local\File\Path\file.txt");
        /// connection.PutStorageItemAsync("container name", file.Open(FileMode.Open), "RemoteFileName.txt", metadata);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to put the storage object in</param>
        /// <param name="remoteStorageItemName">The alternate name as it will be called on cloudfiles</param>
        /// <param name="storageStream">The stream representing the storage item to upload</param>
        /// <param name="metadata">An optional parameter containing a dictionary of meta tags to associate with the storage object</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PutStorageItemAsync(string containerName, Stream storageStream, string remoteStorageItemName, Dictionary<string, string> metadata)
        {
            var thread = new Thread(
                () =>
                    {
                        try
                        {
                            PutStorageItem(containerName, storageStream, remoteStorageItemName, metadata);
                        }
                        finally
                        {
                            if (OperationComplete != null)
                            {
                                //Fire the operation complete event if there are any listeners
                                OperationComplete();
                            }
                        }
                    }
                );
            thread.Start();
        }

        /// <summary>
        /// This method uploads a storage object to cloudfiles asychronously
        /// </summary>
        /// <example>
        /// <code>
        /// private void transferComplete()
        /// {
        ///     if (InvokeRequired)
        ///     {
        ///         Invoke(new CloseCallback(Close), new object[]{});
        ///     }
        ///     else
        ///     {
        ///         if (!IsDisposed)
        ///             Close();
        ///     }
        /// }
        /// 
        /// private void fileTransferProgress(int bytesTransferred)
        /// {
        ///    if (InvokeRequired)
        ///    {
        ///        Invoke(new FileProgressCallback(fileTransferProgress), new object[] {bytesTransferred});
        ///    }
        ///    else
        ///    {
        ///        System.Console.WriteLine(totalTransferred.ToString());
        ///        totalTransferred += bytesTransferred;
        ///        bytesTransferredLabel.Text = totalTransferred.ToString();
        ///        var progress = (int) ((totalTransferred/filesize)*100.0f);
        ///        if(progress > 100)
        ///            progress = 100;
        ///        transferProgressBar.Value = progress ;
        ///    }
        /// }
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.PutStorageItemAsync("container name", "LocalFileName.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to put the storage object in</param>
        /// <param name="localStorageItemName">The name of the file locally </param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PutStorageItemAsync(string containerName, string localStorageItemName)
        {
            var thread = new Thread(
                () =>
                    {
                        try
                        {
                            PutStorageItem(containerName, localStorageItemName);
                        }
                        finally //Always fire the completed event
                        {
                            if (OperationComplete != null)
                            {
                                //Fire the operation complete event if there aren't any listeners
                                OperationComplete();
                            }
                        }
                    }
                );
            thread.Start();
        }

        /// <summary>
        /// This method uploads a storage object to cloudfiles with an alternate name
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Dictionary{string, string} metadata = new Dictionary{string, string}();
        /// metadata.Add("key1", "value1");
        /// metadata.Add("key2", "value2");
        /// metadata.Add("key3", "value3");
        /// FileInfo file = new FileInfo("C:\Local\File\Path\file.txt");
        /// connection.PutStorageItem("container name", file.Open(FileMode.Open), "RemoteFileName.txt", metadata);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to put the storage object in</param>
        /// <param name="storageStream">The file stream to upload</param>
        /// <param name="metadata">An optional parameter containing a dictionary of meta tags to associate with the storage object</param>
        /// <param name="remoteStorageItemName">The name of the storage object as it will be called on cloudfiles</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PutStorageItem(string containerName, Stream storageStream, string remoteStorageItemName, Dictionary<string, string> metadata)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(remoteStorageItemName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Putting storage item stream into container '" + containerName + "' named " + remoteStorageItemName + " for user " + _usercreds.Username)
                .ThenDoing(() => putStorageItem(containerName, storageStream, remoteStorageItemName, metadata))
                .AndIfErrorThrownIs<WebException>()
                .Do(ex => DetermineReasonForStorageItemError(ex, true))
                .AndLogError("Error putting storage item stream into container '" + containerName + "' named " + remoteStorageItemName + " for user " + _usercreds.Username)
                .Now();

        }

        /// <summary>
        /// This method deletes a storage object in a given container
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.DeleteStorageItem("container name", "RemoteStorageItem.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container that contains the storage object</param>
        /// <param name="storageItemName">The name of the storage object to delete</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void DeleteStorageItem(string containerName, string storageItemName)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(storageItemName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Deleting storage item "+ storageItemName + " in container '"+ containerName + "' for user "+ _usercreds.Username)
                .ThenDoing(() => deleteStorageItem(containerName, storageItemName))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForStorageItemError)
                .AndLogError("Error deleting storage item "+ storageItemName + " in container '"+ containerName + "' for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method deletes a storage object in a given container
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.DeleteStorageItem("container name", "RemoteStorageItem.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container that contains the storage object</param>
        /// <param name="storageItemName">The name of the storage object to delete</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PurgePublicStorageItem(string containerName, string storageItemName)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(storageItemName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Deleting storage item " + storageItemName + " in container '" + containerName + "' for user " + _usercreds.Username)
                .ThenDoing(() => deleteStorageItem(containerName, storageItemName))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForStorageItemError)
                .AndLogError("Error deleting storage item " + storageItemName + " in container '" + containerName + "' for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method deletes a storage object in a given public container
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.DeleteStorageItem("container name", "RemoteStorageItem.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the public container that contains the storage object</param>
        /// <param name="storageItemName">The name of the storage object to delete</param>
        /// <param name="emailAddresses">The email addresses to notify once the purge is complete</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void PurgePublicStorageItem(string containerName, string storageItemName, string[] emailAddresses)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(storageItemName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Deleting storage item " + storageItemName + " in container '" + containerName + "' for user " + _usercreds.Username)
                .ThenDoing(() => purgePublicStorageItem(containerName, storageItemName, emailAddresses))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForStorageItemError)
                .AndLogError("Error deleting storage item " + storageItemName + " in container '" + containerName + "' for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method downloads a storage object from cloudfiles
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// StorageItem storageItem = connection.GetStorageItem("container name", "RemoteStorageItem.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container that contains the storage object to retrieve</param>
        /// <param name="storageItemName">The name of the storage object to retrieve</param>
        /// <returns>An instance of StorageItem with the stream containing the bytes representing the desired storage object</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public StorageItem GetStorageItem(string containerName, string storageItemName)
        {
            return GetStorageItem(containerName, storageItemName, new Dictionary<RequestHeaderFields, string>());
        }

        /// <summary>
        /// An alternate method for downloading storage objects. This one allows specification of special HTTP 1.1 compliant GET headers
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials); 
        /// Dictionary{RequestHeaderFields, string} requestHeaderFields = Dictionary{RequestHeaderFields, string}();
        /// string dummy_etag = "5c66108b7543c6f16145e25df9849f7f";
        /// requestHeaderFields.Add(RequestHeaderFields.IfMatch, dummy_etag);
        /// requestHeaderFields.Add(RequestHeaderFields.IfNoneMatch, dummy_etag);
        /// requestHeaderFields.Add(RequestHeaderFields.IfModifiedSince, DateTime.Now.AddDays(6).ToString());
        /// requestHeaderFields.Add(RequestHeaderFields.IfUnmodifiedSince, DateTime.Now.AddDays(-6).ToString());
        /// requestHeaderFields.Add(RequestHeaderFields.Range, "0-5");
        /// StorageItem storageItem = connection.GetStorageItem("container name", "RemoteStorageItem.txt", requestHeaderFields);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container that contains the storage object</param>
        /// <param name="storageItemName">The name of the storage object</param>
        /// <param name="requestHeaderFields">A dictionary containing the special headers and their values</param>
        /// <returns>An instance of StorageItem with the stream containing the bytes representing the desired storage object</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public StorageItem GetStorageItem(string containerName, string storageItemName, Dictionary<RequestHeaderFields, string> requestHeaderFields)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(storageItemName))
                throw new ArgumentNullException();

            return StartProcess
                .ByLoggingMessage("Getting storage item " + storageItemName + " with in container '" + containerName + "' for user " + _usercreds.Username)
                .ThenDoing(() => getStorageItem(containerName, storageItemName, requestHeaderFields))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForStorageItemError)
                .AndLogError("Error getting storage item " + storageItemName + " with request Header fields in container '" + containerName + "' for user " + _usercreds.Username)
                .Now();
        }


        /// <summary>
        /// This method downloads a storage object from cloudfiles asychronously
        /// </summary>
        /// <example>
        /// <code>
        /// private void transferComplete()
        /// {
        ///     if (InvokeRequired)
        ///     {
        ///         Invoke(new CloseCallback(Close), new object[]{});
        ///     }
        ///     else
        ///     {
        ///         if (!IsDisposed)
        ///             Close();
        ///     }
        /// }
        /// 
        /// private void fileTransferProgress(int bytesTransferred)
        /// {
        ///    if (InvokeRequired)
        ///    {
        ///        Invoke(new FileProgressCallback(fileTransferProgress), new object[] {bytesTransferred});
        ///    }
        ///    else
        ///    {
        ///        System.Console.WriteLine(totalTransferred.ToString());
        ///        totalTransferred += bytesTransferred;
        ///        bytesTransferredLabel.Text = totalTransferred.ToString();
        ///        var progress = (int) ((totalTransferred/filesize)*100.0f);
        ///        if(progress > 100)
        ///            progress = 100;
        ///        transferProgressBar.Value = progress ;
        ///    }
        /// }
        /// 
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.AddProgressWatcher(fileTransferProgress);
        /// connection.OperationComplete += transferComplete;
        /// connection.GetStorageItemAsync("container name", "RemoteStorageItem.txt", "RemoteStorageItem.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container that contains the storage object to retrieve</param>
        /// <param name="storageItemName">The name of the storage object to retrieve</param>
        /// <param name="localFileName">The name to write the file to on your hard drive. </param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void GetStorageItemAsync(string containerName, string storageItemName, string localFileName)
        {
            var thread = new Thread(
                () =>
                    {
                        try
                        {
                            GetStorageItem(containerName, storageItemName, localFileName);
                        }
                        finally //Always fire the completed event
                        {
                            if (OperationComplete != null)
                            {
                                //Fire the operation complete event if there aren't any listeners
                                OperationComplete();
                            }
                        }
                    }
                );
            thread.Start();
        }

        /// <summary>
        /// This method downloads a storage object from cloudfiles asychronously
        /// </summary>
        /// <example>
        /// <code>
        /// private void transferComplete()
        /// {
        ///     if (InvokeRequired)
        ///     {
        ///         Invoke(new CloseCallback(Close), new object[]{});
        ///     }
        ///     else
        ///     {
        ///         if (!IsDisposed)
        ///             Close();
        ///     }
        /// }
        /// 
        /// private void fileTransferProgress(int bytesTransferred)
        /// {
        ///    if (InvokeRequired)
        ///    {
        ///        Invoke(new FileProgressCallback(fileTransferProgress), new object[] {bytesTransferred});
        ///    }
        ///    else
        ///    {
        ///        System.Console.WriteLine(totalTransferred.ToString());
        ///        totalTransferred += bytesTransferred;
        ///        bytesTransferredLabel.Text = totalTransferred.ToString();
        ///        var progress = (int) ((totalTransferred/filesize)*100.0f);
        ///        if(progress > 100)
        ///            progress = 100;
        ///        transferProgressBar.Value = progress ;
        ///    }
        /// }
        /// Dictionary{RequestHeaderFields, string} requestHeaderFields = Dictionary{RequestHeaderFields, string}();
        /// string dummy_etag = "5c66108b7543c6f16145e25df9849f7f";
        /// requestHeaderFields.Add(RequestHeaderFields.IfMatch, dummy_etag);
        /// requestHeaderFields.Add(RequestHeaderFields.IfNoneMatch, dummy_etag);
        /// requestHeaderFields.Add(RequestHeaderFields.IfModifiedSince, DateTime.Now.AddDays(6).ToString());
        /// requestHeaderFields.Add(RequestHeaderFields.IfUnmodifiedSince, DateTime.Now.AddDays(-6).ToString());
        /// requestHeaderFields.Add(RequestHeaderFields.Range, "0-5");
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.AddProgressWatcher(fileTransferProgress);
        /// connection.OperationComplete += transferComplete;
        /// connection.GetStorageItemAsync("container name", "RemoteStorageItem.txt", "RemoteStorageItem.txt", requestHeaderFields);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container that contains the storage object to retrieve</param>
        /// <param name="storageItemName">The name of the storage object to retrieve</param>
        /// <param name="localFileName">The name to write the file to on your hard drive. </param>
        /// <param name="requestHeaderFields">A dictionary containing the special headers and their values</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void GetStorageItemAsync(string containerName, string storageItemName, string localFileName, Dictionary<RequestHeaderFields, string> requestHeaderFields)
        {
            var thread = new Thread(
                () =>
                    {
                        try
                        {
                            GetStorageItem(containerName, storageItemName, localFileName, requestHeaderFields);
                        }
                        finally //Always fire the completed event
                        {
                            if (OperationComplete != null)
                            {
                                //Fire the operation complete event if there aren't any listeners
                                OperationComplete();
                            }
                        }
                    }
                );
            thread.Start();
        }

        /// <summary>
        /// An alternate method for downloading storage objects from cloudfiles directly to a file name specified in the method
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// StorageItem storageItem = connection.GetStorageItem("container name", "RemoteStorageItem.txt", "C:\Local\File\Path\file.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container that contains the storage object to retrieve</param>
        /// <param name="storageItemName">The name of the storage object to retrieve</param>
        /// <param name="localFileName">The file name to save the storage object into on disk</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void GetStorageItem(string containerName, string storageItemName, string localFileName)
        {
            GetStorageItem(containerName, storageItemName, localFileName, new Dictionary<RequestHeaderFields, string>());
        }

        /// <summary>
        /// An alternate method for downloading storage objects from cloudfiles directly to a file name specified in the method
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Dictionary{RequestHeaderFields, string} requestHeaderFields = Dictionary{RequestHeaderFields, string}();
        /// string dummy_etag = "5c66108b7543c6f16145e25df9849f7f";
        /// requestHeaderFields.Add(RequestHeaderFields.IfMatch, dummy_etag);
        /// requestHeaderFields.Add(RequestHeaderFields.IfNoneMatch, dummy_etag);
        /// requestHeaderFields.Add(RequestHeaderFields.IfModifiedSince, DateTime.Now.AddDays(6).ToString());
        /// requestHeaderFields.Add(RequestHeaderFields.IfUnmodifiedSince, DateTime.Now.AddDays(-6).ToString());
        /// requestHeaderFields.Add(RequestHeaderFields.Range, "0-5");
        /// StorageItem storageItem = connection.GetStorageItem("container name", "RemoteFileName.txt", "C:\Local\File\Path\file.txt", requestHeaderFields);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container that contains the storage object to retrieve</param>
        /// <param name="storageItemName">The name of the storage object to retrieve</param>
        /// <param name="localFileName">The file name to save the storage object into on disk</param>
        /// <param name="requestHeaderFields">A dictionary containing the special headers and their values</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void GetStorageItem(string containerName, string storageItemName, string localFileName, Dictionary<RequestHeaderFields, string> requestHeaderFields)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(storageItemName) ||
                string.IsNullOrEmpty(localFileName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Getting storage item "+ storageItemName + " in container '"+ containerName + "' for user "+ _usercreds.Username + " and name it "+ localFileName + " locally")
                .ThenDoing(() => getStorageItem(containerName, storageItemName, localFileName, requestHeaderFields))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForStorageItemError)
                .AndLogError("Error getting storage item "+ storageItemName + " with request Header fields in container '"+ containerName + "' for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method applies meta tags to a storage object on cloudfiles
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Dictionary{string, string} metadata = new Dictionary{string, string}();
        /// metadata.Add("key1", "value1");
        /// metadata.Add("key2", "value2");
        /// metadata.Add("key3", "value3");
        /// connection.SetStorageItemMetaInformation("container name", "C:\Local\File\Path\file.txt", metadata);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container containing the storage object</param>
        /// <param name="storageItemName">The name of the storage object</param>
        /// <param name="metadata">A dictionary containiner key/value pairs representing the meta data for this storage object</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void SetStorageItemMetaInformation(string containerName, string storageItemName, Dictionary<string, string> metadata)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(storageItemName))
                throw new ArgumentNullException();

            StartProcess
                .ByLoggingMessage("Setting storage item "+ storageItemName + " meta information for container '"+ containerName + "' for user")
                .ThenDoing(() => setStorageItemMetaInformation(containerName, storageItemName, metadata))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForStorageItemError)
                .AndLogError("Error setting metainformation for storage item "+ storageItemName + " in container '"+ containerName + "' for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method retrieves meta information and size, in bytes, of a requested storage object
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// StorageItem storageItem = connection.GetStorageItemInformation("container name", "RemoteStorageItem.txt");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container that contains the storage object</param>
        /// <param name="storageItemName">The name of the storage object</param>
        /// <returns>An instance of StorageItem containing the byte size and meta information associated with the container</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public StorageItemInformation GetStorageItemInformation(string containerName, string storageItemName)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(storageItemName))
                throw new ArgumentNullException();

            return StartProcess
                .ByLoggingMessage("Getting storage item " + storageItemName + " information in container '" + containerName + "' for user")
                .ThenDoing(() => getStorageItemInformation(containerName, storageItemName))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForStorageItemError)
                .AndLogError("Error getting storage item " + storageItemName + " information in container '" + containerName + "' for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method retrieves the names of the of the containers made public on the CDN
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// List{string} containers = connection.GetPublicContainers();
        /// </code>
        /// </example>
        /// <returns>A list of the public containers</returns>
        public List<string> GetPublicContainers()
        {
            return StartProcess
                .ByLoggingMessage("Getting public containers for user " + _usercreds.Username)
                .ThenDoing(() => getPublicContainers())
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error getting public containers for user " + _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method sets a container as public on the CDN
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Uri containerPublicUrl = connection.MarkContainerAsPublic("container name", 12345);
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to mark public</param>
        /// <param name="timeToLiveInSeconds">The maximum time (in seconds) content should be kept alive on the CDN before it checks for freshness.</param>
        /// <returns>A string representing the URL of the public container or null</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public Uri MarkContainerAsPublic(string containerName, int timeToLiveInSeconds)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            return StartProcess.ByLoggingMessage("Marking container '"+ containerName + "' as public with TTL of "+ timeToLiveInSeconds + " seconds for user "+ _usercreds.Username)
                .ThenDoing(() => markContainerAsPublic(containerName, timeToLiveInSeconds))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error marking container '"+ containerName + "' as public with TTL of "+ timeToLiveInSeconds + " seconds for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// This method sets a container as public on the CDN
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Uri containerPublicUrl = connection.MarkContainerAsPublic("container name");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to mark public</param>
        /// <returns>A string representing the URL of the public container or null</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public Uri MarkContainerAsPublic(string containerName)
        {
            return MarkContainerAsPublic(containerName, -1);
        }

        /// <summary>
        /// This method sets a container as private on the CDN
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// connection.MarkContainerAsPrivate("container name");
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to mark public</param>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public void MarkContainerAsPrivate(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            StartProcess.ByLoggingMessage("Marking container "+ containerName + " as private for user "+ _usercreds.Username)
                .ThenDoing(() => markContainerAsPrivate(containerName))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error marking container "+ containerName + " as private for user "+ _usercreds.Username)
                .Now();
        }


        /// <summary>
        /// Retrieves a Container object containing the public CDN information
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// Container container = connection.GetPublicContainerInformation("container name")
        /// </code>
        /// </example>
        /// <param name="containerName">The name of the container to query about</param>
        /// <returns>An instance of Container with appropriate CDN information or null</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of the reference parameters are null</exception>
        public Container GetPublicContainerInformation(string containerName)
        {
            if (!HasCDN())
                return null;

            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentNullException();

            return StartProcess.ByLoggingMessage("Getting public container " + containerName + " information for user " + _usercreds.Username)
                .ThenDoing(() => getPublicContainerInformation(containerName))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error getting public container "+ containerName + " information for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="publiccontainer"></param>
        /// <param name="loggingenabled"></param>
        /// <param name="ttl"></param>
        public void SetDetailsOnPublicContainer(string publiccontainer, bool loggingenabled, int ttl)
        {
            if (string.IsNullOrEmpty(publiccontainer))
                throw new ArgumentNullException();

            StartProcess.ByLoggingMessage("Adding logging to container named "+ publiccontainer + " for user "+ _usercreds.Username)
                .ThenDoing(() => setDetailsOnPublicContainer(publiccontainer, loggingenabled, ttl))
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Error setting logging on container named "+ publiccontainer + " for user "+ _usercreds.Username)
                .Now();
        }

        /// <summary>
        /// XML serialized format of the public account information
        /// </summary>
        /// <example>
        /// <code>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// XmlDocument xmlResponse = connection.GetPublicAccountInformationXML();
        /// </code>
        /// </example>
        /// <returns>xml document of public account information</returns>
        public XmlDocument GetPublicAccountInformationXML()
        {
            if (!HasCDN())
                return null;

            return StartProcess.ByLoggingMessage("Retrieving account information for account " + CdnManagementUrl)
                .ThenDoing(() => getPublicAccountInformationXML())
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Failed to get account information for account " + CdnManagementUrl)
                .Now();
        }

        /// <summary>
        /// JSON serialized format of the public account information
        /// </summary>
        /// UserCredentials userCredentials = new UserCredentials("username", "api key");
        /// IConnection connection = new Connection(userCredentials);
        /// string jsonResponse = connection.GetPublicAccountInformationJSON();
        /// <returns>json string of public account information</returns>
        public string GetPublicAccountInformationJSON()
        {
            if (!HasCDN())
                return null;

            return StartProcess.ByLoggingMessage("Retrieving account information for account " + CdnManagementUrl)
                .ThenDoing(() => getPublicAccountInformationJson())
                .AndIfErrorThrownIs<WebException>()
                .Do(DetermineReasonForContainerError)
                .AndLogError("Failed to get account information for account " + CdnManagementUrl)
                .Now();
        }

        //PRIVATE METHODS
        //TODO: extract to service

        private void markContainerAsPrivate(string containerName)
        {
            if (!HasCDN())
                return;
            var request = new SetPublicContainerDetails(CdnManagementUrl, containerName, false, false, -1);
            _requestfactory.Submit(request, AuthToken);
        }

        private Uri markContainerAsPublic(string containerName, int timeToLiveInSeconds)
        {
            if (!HasCDN())
                return null;

            var request = new MarkContainerAsPublic(CdnManagementUrl, containerName, timeToLiveInSeconds);
            var response = _requestfactory.Submit(request, AuthToken);

            return response == null ? null : new Uri(response.Headers[Constants.X_CDN_URI]);
        }

        private void MakeStorageDirectory(string containerName, string remoteobjname)
        {
            if (string.IsNullOrEmpty(containerName) ||
                string.IsNullOrEmpty(remoteobjname))
                throw new ArgumentNullException();

            Log.Debug(this, "Putting storage item "
                           + remoteobjname + " with metadata into container '"
                           + containerName + "' for user "
                           + _usercreds.Username);

            try
            {
                var makedirectory = new PutStorageDirectory(StorageUrl, containerName, remoteobjname);
                _requestfactory.Submit(makedirectory, AuthToken, _usercreds.ProxyCredentials);
            }
            catch (WebException webException)
            {
                Log.Error(this, "Error putting storage item "
                                + remoteobjname + " with metadata into container '"
                                + containerName + "' for user "
                                + _usercreds.Username, webException);

                var webResponse = (HttpWebResponse)webException.Response;
                if (webResponse == null) throw;
                if (webResponse.StatusCode == HttpStatusCode.BadRequest)
                    throw new ContainerNotFoundException("The requested container does not exist");
                if (webResponse.StatusCode == HttpStatusCode.PreconditionFailed)
                    throw new PreconditionFailedException(webException.Message);

                throw;
            }
        }

        private void Authenticate()
        {
            StartProcess.
                ByLoggingMessage("Authenticating user " + _usercreds.Username).
                ThenDoing(AuthenticateSequence).
                AndIfErrorThrownIs<Exception>().
                Do(Nothing).
                AndLogError("Error authenticating user " + _usercreds.Username).
                Now();
        }

        private bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(AuthToken) && !string.IsNullOrEmpty(StorageUrl) && _usercreds != null;
        }

        private string GetContainerCdnUri(Container container)
        {
            if (!HasCDN())
                return null;

            try
            {
                var publicContainer = GetPublicContainerInformation(container.Name);
                return publicContainer == null ? "" : publicContainer.CdnUri;
            }
            catch (ContainerNotFoundException)
            {
                return "";
            }
            catch (WebException we)
            {
                Log.Error(this, "Error getting container CDN Uril from getContainerInformation for container '"
                                + container.Name + "' for user "
                                + _usercreds.Username, we);

                var response = (HttpWebResponse)we.Response;
                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new AuthenticationFailedException(we.Message);
                throw;
            }
        }

        private Dictionary<string, string> GetMetadata(IResponse getStorageItemResponse)
        {
            var headers = getStorageItemResponse.Headers;
            return headers.AllKeys
                .Where(key => key.IndexOf(Constants.META_DATA_HEADER) > -1)
                .ToDictionary(key => key, key => headers[key]);
        }

        private void StoreFile(string filename, Stream contentStream)
        {
            using (var file = File.Create(filename))
            {
                contentStream.WriteTo(file);
            }
        }

        private string BuildAccountJson()
        {
            string jsonResponse = "";
            var getAccountInformationJson = new GetAccountInformationSerialized(StorageUrl, Format.JSON);
            var getAccountInformationJsonResponse = _requestfactory.Submit(getAccountInformationJson, AuthToken);

            if (getAccountInformationJsonResponse.ContentBody.Count > 0)
                jsonResponse = String.Join("", getAccountInformationJsonResponse.ContentBody.ToArray());

            getAccountInformationJsonResponse.Dispose();
            return jsonResponse;
        }

        private AccountInformation BuildAccount()
        {
            var getAccountInformation = new GetAccountInformation(StorageUrl);
            var getAccountInformationResponse = _requestfactory.Submit(getAccountInformation, AuthToken);
            return new AccountInformation(getAccountInformationResponse.Headers[Constants.X_ACCOUNT_CONTAINER_COUNT], getAccountInformationResponse.Headers[Constants.X_ACCOUNT_BYTES_USED]);
        }

        private void AuthenticateSequence()
        {
            var getAuthentication = new GetAuthentication(_usercreds);
            var getAuthenticationResponse = _requestfactory.Submit(getAuthentication);
            // var getAuthenticationResponse = getAuthentication.Apply(request);

            if (getAuthenticationResponse.Status == HttpStatusCode.NoContent)
            {
                StorageUrl = getAuthenticationResponse.Headers[Constants.X_STORAGE_URL];
                if (_useServiceNet)
                    StorageUrl = StorageUrl.MakeServiceNet();

                AuthToken = getAuthenticationResponse.Headers[Constants.X_AUTH_TOKEN];
                CdnManagementUrl = getAuthenticationResponse.Headers[Constants.X_CDN_MANAGEMENT_URL];
                return;
            }

            if (!_retry && getAuthenticationResponse.Status == HttpStatusCode.Unauthorized)
            {
                _retry = true;
                Authenticate();
                return;
            }
        }

        private XmlDocument BuildAccountXml()
        {
            var accountInformationXml = new GetAccountInformationSerialized(StorageUrl, Format.XML);
            var getAccountInformationXmlResponse = _requestfactory.Submit(accountInformationXml, AuthToken);

            if (getAccountInformationXmlResponse.ContentBody.Count == 0)
            {
                return new XmlDocument();
            }
            var contentBody = String.Join("", getAccountInformationXmlResponse.ContentBody.ToArray());

            getAccountInformationXmlResponse.Dispose();

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(contentBody);
                return doc;
            }
            catch (XmlException)
            {
                return new XmlDocument();
            }
        }

        private void ContainerCreation(string containername)
        {
            var createContainer = new CreateContainer(StorageUrl, containername);
            var createContainerResponse = _requestfactory.Submit(createContainer, AuthToken);
            if (createContainerResponse.Status == HttpStatusCode.Accepted)
                throw new ContainerAlreadyExistsException("The container already exists");
        }

        private void deleteContainer(string url, string containerName)
        {
            var deleteContainer = new DeleteContainer(url, containerName, null);
            _requestfactory.Submit(deleteContainer, AuthToken, _usercreds.ProxyCredentials);
        }

        private void purgePublicContainer(string url, string containerName, string[] emailAddresses)
        {
            var deleteContainer = new DeleteContainer(url, containerName, emailAddresses);
            _requestfactory.Submit(deleteContainer, AuthToken, _usercreds.ProxyCredentials);
        }

        private List<string> BuildContainerList()
        {
            return BuildContainerList(null);
        }

        private List<string> BuildContainerList(Dictionary<GetListParameters, string> parameters)
        {
            IList<string> containerList = new List<string>();
            var getContainers = new GetContainers(StorageUrl, parameters);
            var getContainersResponse = _requestfactory.Submit(getContainers, AuthToken, _usercreds.ProxyCredentials);
            if (getContainersResponse.Status == HttpStatusCode.OK)
            {
                containerList = getContainersResponse.ContentBody;
            }
            return containerList.ToList();
        }

        private void DetermineReasonForStorageItemError(WebException ex, bool onContainer)
        {
            var response = (HttpWebResponse)ex.Response;
            if (response != null && response.StatusCode == HttpStatusCode.NotFound)
            {
                if (onContainer)
                    throw new ContainerNotFoundException("The requested container does not exist");
                throw new StorageItemNotFoundException("The requested item does not exist");
            }
            if (response != null && response.StatusCode == HttpStatusCode.Conflict)
                throw new ContainerNotEmptyException("The container you are trying to delete is not empty");
            if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                throw new AuthenticationFailedException(ex.Message);
            if (response != null && response.StatusCode == HttpStatusCode.PreconditionFailed)
                throw new PreconditionFailedException(ex.Message);
            if (response != null && response.StatusCode == HttpStatusCode.BadRequest)
                throw new ContainerNotFoundException("The requested container does not exist");
        }

        private void DetermineReasonForStorageItemError(WebException ex)
        {
            DetermineReasonForStorageItemError(ex, false);
        }

        private void DetermineReasonForContainerError(WebException ex)
        {
            var response = (HttpWebResponse)ex.Response;
            if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                throw new ContainerNotFoundException("The requested container does not exist");
            if (response != null && response.StatusCode == HttpStatusCode.Conflict)
                throw new ContainerNotEmptyException("The container you are trying to delete is not empty");
            if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                throw new AuthenticationFailedException(ex.Message);
            if (response != null && response.StatusCode == HttpStatusCode.PreconditionFailed)
                throw new PreconditionFailedException(ex.Message);
            if (response != null && response.StatusCode == HttpStatusCode.BadRequest)
                throw new ContainerNotFoundException("The requested container does not exist");

        }

        private void makePath(string containerName, string path)
        {
            var directories = path.StripSlashPrefix().Split('/');
            var directory = "";
            var firstItem = true;
            foreach (var item in directories)
            {
                if (string.IsNullOrEmpty(item)) continue;
                if (!firstItem) directory += "/";
                directory += item.Encode();
                MakeStorageDirectory(containerName, directory);
                firstItem = false;
            }
        }

        private List<string> getContainerItemList(string containerName, Dictionary<GetListParameters, string> parameters)
        {
            var containerItemList = new List<string>();
            var getContainerItemList = new GetContainerItemList(StorageUrl, containerName, parameters);
            var getContainerItemListResponse = _requestfactory.Submit(getContainerItemList, AuthToken, _usercreds.ProxyCredentials);
            if (getContainerItemListResponse.Status == HttpStatusCode.OK)
            {
                containerItemList.AddRange(getContainerItemListResponse.ContentBody);
            }
            return containerItemList;
        }

        private Container getContainerInformation(string containerName)
        {
            var getContainerInformation = new GetContainerInformation(StorageUrl, containerName);
            var getContainerInformationResponse = _requestfactory.Submit(getContainerInformation, AuthToken, _usercreds.ProxyCredentials);
            var container = new Container(containerName)
            {
                ByteCount =
                    long.Parse(
                    getContainerInformationResponse.Headers[Constants.X_CONTAINER_BYTES_USED]),
                ObjectCount =
                    long.Parse(
                    getContainerInformationResponse.Headers[
                        Constants.X_CONTAINER_STORAGE_OBJECT_COUNT])
            };
            var url = GetContainerCdnUri(container);
            if (!string.IsNullOrEmpty(url)) url += "/";
            container.CdnUri = url;
            return container;
        }

        private string getContainerInformationJson(string containerName)
        {
            var getContainerInformation = new GetContainerInformationSerialized(StorageUrl, containerName, Format.JSON);
            var getSerializedResponse = _requestfactory.Submit(getContainerInformation, AuthToken, _usercreds.ProxyCredentials);
            var jsonResponse = String.Join("", getSerializedResponse.ContentBody.ToArray());
            getSerializedResponse.Dispose();
            return jsonResponse;
        }

        private XmlDocument getContainerInformationXml(string containerName)
        {
            var getContainerInformation = new GetContainerInformationSerialized(StorageUrl, containerName, Format.XML);
            var getSerializedResponse = _requestfactory.Submit(getContainerInformation, AuthToken, _usercreds.ProxyCredentials);
            var xmlResponse = String.Join("", getSerializedResponse.ContentBody.ToArray());
            getSerializedResponse.Dispose();

            var xmlDocument = new XmlDocument();
            try
            {
                xmlDocument.LoadXml(xmlResponse);
            }
            catch (XmlException)
            {
                return xmlDocument;
            }

            return xmlDocument;
        }

        private void putStorageItem(string containerName, string localFilePath, Dictionary<string, string> metadata)
        {
            var remoteName = Path.GetFileName(localFilePath);
            var localName = localFilePath.Replace("/", "\\");
            var putStorageItem = new PutStorageItem(StorageUrl, containerName, remoteName, localName, metadata);
            foreach (var callback in _callbackFuncs)
            {
                putStorageItem.Progress += callback;
            }
            _requestfactory.Submit(putStorageItem, AuthToken, _usercreds.ProxyCredentials);
        }

        private void putStorageItem(string containerName, Stream storageStream, string remoteStorageItemName, Dictionary<string, string> metadata)
        {
            var putStorageItem = new PutStorageItem(StorageUrl, containerName, remoteStorageItemName, storageStream, metadata);
            foreach (var callback in _callbackFuncs)
            {
                putStorageItem.Progress += callback;
            }
            _requestfactory.Submit(putStorageItem, AuthToken, _usercreds.ProxyCredentials);
        }

        private Container getPublicContainerInformation(string containerName)
        {
            var request = new GetPublicContainerInformation(CdnManagementUrl, containerName);
            var response = _requestfactory.Submit(request, AuthToken);

            if (!HasCDN())
            return null;

            return response == null ? null
                       : new Container(containerName)
                             {
                                 CdnUri = response.Headers[Constants.X_CDN_URI], 
                                 CdnSslUri = response.Headers[Constants.X_CDN_SSL_URI], 
                                 TTL = Convert.ToInt32(response.Headers[Constants.X_CDN_TTL])
                             };
        }

        private string getPublicAccountInformationJson()
        {
            if (!HasCDN())
                return null;

            var request = new GetPublicContainersInformationSerialized(CdnManagementUrl, Format.JSON);
            var getSerializedResponse = _requestfactory.Submit(request, AuthToken);
            return string.Join("", getSerializedResponse.ContentBody.ToArray());
        }

        private XmlDocument getPublicAccountInformationXML()
        {
            if (!HasCDN())
                return null;

            var request = new GetPublicContainersInformationSerialized(CdnManagementUrl, Format.XML);
            var getSerializedResponse = _requestfactory.Submit(request, AuthToken);
            var xmlResponse = String.Join("", getSerializedResponse.ContentBody.ToArray());
            getSerializedResponse.Dispose();

            var xmlDocument = new XmlDocument();
            try
            {
                xmlDocument.LoadXml(xmlResponse);
            }
            catch (XmlException)
            {
                return xmlDocument;
            }

            return xmlDocument;
        }

        private void setDetailsOnPublicContainer(string publiccontainer, bool loggingenabled, int ttl)
        {
            if (!HasCDN())
                return;
            var request = new SetPublicContainerDetails(CdnManagementUrl, publiccontainer, true, loggingenabled, ttl);
                _requestfactory.Submit(request, AuthToken);
        }

        private void getStorageItem(string containerName, string storageItemName, string localFileName, Dictionary<RequestHeaderFields, string> requestHeaderFields)
        {
            var getStorageItem = new GetStorageItem(StorageUrl, containerName, storageItemName, requestHeaderFields);
            var getStorageItemResponse = _requestfactory.Submit(getStorageItem, AuthToken, _usercreds.ProxyCredentials);
            foreach (var callback in _callbackFuncs)
            {
                getStorageItemResponse.Progress += callback;
            }
            var stream = getStorageItemResponse.GetResponseStream();

            StoreFile(localFileName, stream);
        }

        private void deleteStorageItem(string containerName, string storageItemName)
        {
            var deleteStorageItem = new DeleteStorageItem(StorageUrl, containerName, storageItemName);
            _requestfactory.Submit(deleteStorageItem, AuthToken);
        }

        private void purgePublicStorageItem(string containerName, string storageItemName, string[] emailAddresses)
        {
            var deleteStorageItem = new DeleteStorageItem(CdnManagementUrl, containerName, storageItemName, emailAddresses);
            _requestfactory.Submit(deleteStorageItem, AuthToken);
        }

        private StorageItem getStorageItem(string containerName, string storageItemName, Dictionary<RequestHeaderFields, string> requestHeaderFields)
        {
            var getStorageItem = new GetStorageItem(StorageUrl, containerName, storageItemName, requestHeaderFields);
            var getStorageItemResponse = _requestfactory.Submit(getStorageItem, AuthToken, _usercreds.ProxyCredentials);


            var metadata = GetMetadata(getStorageItemResponse);
            var storageItem = new StorageItem(storageItemName, metadata, getStorageItemResponse.ContentType, getStorageItemResponse.GetResponseStream(), getStorageItemResponse.ContentLength, getStorageItemResponse.LastModified);
            //                getStorageItemResponse.Dispose();
            return storageItem;
        }

        private void setStorageItemMetaInformation(string containerName, string storageItemName, Dictionary<string, string> metadata)
        {
            var setStorageItemInformation = new SetStorageItemMetaInformation(StorageUrl, containerName, storageItemName, metadata);
            _requestfactory.Submit(setStorageItemInformation, AuthToken, _usercreds.ProxyCredentials);
        }

        private StorageItemInformation getStorageItemInformation(string containerName, string storageItemName)
        {
            var getStorageItemInformation = new GetStorageItemInformation(StorageUrl, containerName, storageItemName);
            var getStorageItemInformationResponse = _requestfactory.Submit(getStorageItemInformation, AuthToken, _usercreds.ProxyCredentials);


            var storageItemInformation = new StorageItemInformation(getStorageItemInformationResponse.Headers);

            return storageItemInformation;
        }

        private List<string> getPublicContainers()
        {
            if (!HasCDN())
                return null;

            var getPublicContainers = new GetPublicContainers(CdnManagementUrl);
                var getPublicContainersResponse = _requestfactory.Submit(getPublicContainers, AuthToken);
                var containerList = getPublicContainersResponse.ContentBody;
                getPublicContainersResponse.Dispose();

                return containerList.ToList();
        }
    }
}