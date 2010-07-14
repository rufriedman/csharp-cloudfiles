///
/// See COPYING file for licensing information
///

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using com.mosso.cloudfiles.domain.request.Interfaces;
using com.mosso.cloudfiles.domain.response;
using com.mosso.cloudfiles.domain.response.Interfaces;
using com.mosso.cloudfiles.utils;

namespace com.mosso.cloudfiles.domain.request
{
    /// <summary>
    /// Wraps requests to optionally handle proxy credentials and ssl
    /// </summary>
    public class CloudFilesRequest : ICloudFilesRequest
    {
        private readonly HttpWebRequest _httpWebRequest;
        private readonly ProxyCredentials _proxyCredentials;
        private event Connection.ProgressCallback Progress;

        public void SetContent(Stream stream, Connection.ProgressCallback progress)
        {
            ContentStream = stream;
            ContentLength = stream.Length;
            Progress = progress;

            ETag = StringifyMD5(new MD5CryptoServiceProvider().ComputeHash(ContentStream));
            ContentStream.Seek(0, 0);

        }
        public Stream ContentStream
        {
            get;
            private set;
        }
        /// <summary>
        /// temp
        /// </summary>
        public CloudFilesRequest(Uri uri)
            : this(WebRequest.Create(uri) as HttpWebRequest)
        {

        }
        /// <summary>
        /// Constructor without proxy credentials provided
        /// </summary>
        /// <param name="request">The request being sent to the server</param>
        public CloudFilesRequest(HttpWebRequest request)
            : this(request, null)
        {
        }

        /// <summary>
        /// Constructor with proxy credentials provided
        /// </summary>
        /// <param name="request">The request being sent to the server</param>
        /// <param name="proxyCredentials">Proxy credentials</param>
        /// <exception cref="System.ArgumentNullException">Thrown when any of the reference arguments are null</exception>
        public CloudFilesRequest(HttpWebRequest request, ProxyCredentials proxyCredentials)
        {
            if (request == null) throw new ArgumentNullException();

            _httpWebRequest = request;
            _proxyCredentials = proxyCredentials;
        }

        /// <summary>
        /// RequestType
        /// </summary>
        /// <returns>the type of the request</returns>
        public Type RequestType
        {
            get { return _httpWebRequest.GetType(); }
        }


        public bool SendChunked
        {
            get { return _httpWebRequest.SendChunked; }
            set { _httpWebRequest.SendChunked = value; }
        }

        public Stream GetRequestStream()
        {
            return _httpWebRequest.GetRequestStream();
        }
        /// <summary>
        /// GetRequest
        /// </summary>
        /// <returns>a HttpWebRequest object that has all the information to make a request against CloudFiles</returns>
        public ICloudFilesResponse GetResponse()
        {




            _httpWebRequest.Timeout = Constants.CONNECTION_TIMEOUT;
            _httpWebRequest.UserAgent = Constants.USER_AGENT;

            //   HandleIsModifiedSinceHeaderRequestFieldFor(_httpWebRequest);

            HandleRangeHeader(_httpWebRequest);
            if (_httpWebRequest.ContentLength > 0)
                AttachBodyToWebRequest(_httpWebRequest);
            HandleProxyCredentialsFor(_httpWebRequest);
            return new CloudFilesResponse((HttpWebResponse)_httpWebRequest.GetResponse());

        }


        public Uri RequestUri
        {
            get { return _httpWebRequest.RequestUri; }
        }



        public string Method
        {
            get { return _httpWebRequest.Method; }
            set { _httpWebRequest.Method = value; }
        }

        public WebHeaderCollection Headers
        {
            get
            {



                return _httpWebRequest.Headers;

            }
        }

        public long ContentLength
        {
            get { return _httpWebRequest.ContentLength; }
            private set { _httpWebRequest.ContentLength = value; }
        }

        public int RangeTo
        {
            set;
            get;
        }

        public int RangeFrom
        {
            set;
            get;
        }

        public string ContentType
        {
            get { return _httpWebRequest.ContentType; }
            set { _httpWebRequest.ContentType = value; }
        }

        public DateTime IfModifiedSince
        {
            get { return _httpWebRequest.IfModifiedSince; }
            set { _httpWebRequest.IfModifiedSince = value; }
        }

        public string ETag
        {
            get { return Headers[Constants.ETAG]; }
            private set { Headers.Add(Constants.ETAG, value); }
        }

        public bool AllowWriteStreamBuffering
        {
            get { return _httpWebRequest.AllowWriteStreamBuffering; }
            set { _httpWebRequest.AllowWriteStreamBuffering = value; }
        }

        public string UserAgent
        {
            get { return _httpWebRequest.UserAgent; }
            set { _httpWebRequest.UserAgent = value; }
        }

        private void HandleRangeHeader(HttpWebRequest webrequest)
        {

            if (RangeFrom != 0 && RangeTo == 0)
                webrequest.AddRange("bytes", RangeFrom);
            else if (RangeFrom == 0 && RangeTo != 0)
                webrequest.AddRange("bytes", RangeTo);
            else if (RangeFrom != 0 && RangeTo != 0)
                webrequest.AddRange("bytes", RangeFrom, RangeTo);
        }


        private void HandleProxyCredentialsFor(WebRequest httpWebRequest)
        {
            if (_proxyCredentials == null) return;

            var loProxy = new WebProxy(_proxyCredentials.ProxyAddress, true);

            if (_proxyCredentials.ProxyUsername.Length > 0)
                loProxy.Credentials = new NetworkCredential(_proxyCredentials.ProxyUsername, _proxyCredentials.ProxyPassword, _proxyCredentials.ProxyDomain);
            httpWebRequest.Proxy = loProxy;
        }
        private void AttachBodyToWebRequest(WebRequest request)
        {
            using (var webstream = request.GetRequestStream())
            {
                var buffer = new byte[Constants.CHUNK_SIZE];

                int amt;
                while ((amt = ContentStream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    webstream.Write(buffer, 0, amt);

                    //Fire the progress event
                    if (Progress != null)
                    {
                        Progress(amt);
                    }
                }

                ContentStream.Close();
                webstream.Flush();
            }

        }
        private static string StringifyMD5(IEnumerable<byte> bytes)
        {
            var result = new StringBuilder();
            foreach (byte b in bytes)
                result.AppendFormat("{0:x2}", b);
            return result.ToString();
        }
        //        private void HandleRequestBodyFor(HttpWebRequest httpWebRequest)
        //        {
        //           
        //            httpWebRequest.AllowWriteStreamBuffering = false;
        //            if(httpWebRequest.ContentLength < 1)
        //                httpWebRequest.SendChunked = true;
        //
        //            var requestMimeType = _httpWebRequest.ContentType;
        //            httpWebRequest.ContentType = String.IsNullOrEmpty(requestMimeType) 
        //                ? "application/octet-stream" : requestMimeType;
        //
        //            //var stream = httpWebRequest.GetRequestStream();
        //            
        //            //request.ReadFileIntoRequest(stream); //commented by ryan
        //        }


    }
}