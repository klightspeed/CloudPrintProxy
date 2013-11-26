using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System.Net;
using Newtonsoft.Json;

namespace TSVCEO.CloudPrint.Util
{
    public class MIMEPostDataFile
    {
        public string Filename;
        public string ContentType;
        public byte[] Data;
    }

    public static class HTTPHelper
    {

        private static void WriteMultiPartPostData(TextWriter writer, IDictionary<string, string> postdata, string boundary)
        {
            if (postdata != null)
            {
                foreach (KeyValuePair<string, string> kvp in postdata)
                {
                    writer.WriteLine("--{0}", boundary);
                    writer.WriteLine("Content-Disposition: form-data; name=\"{0}\"", Uri.EscapeDataString(kvp.Key));
                    writer.WriteLine();
                    writer.WriteLine(kvp.Value);
                }
            }

            writer.WriteLine("--{0}--", boundary);
            writer.Flush();
        }

        private static void WriteMultiPartPostData(TextWriter writer, object postdata, string boundary)
        {
            WriteMultiPartPostData(writer, TypeDescriptor.GetProperties(postdata).OfType<PropertyDescriptor>().ToDictionary(prop => prop.Name, prop => prop.GetValue(postdata).ToString()), boundary);
        }

        private static void WriteUrlEncodedPostData(TextWriter writer, Dictionary<string, string> postdata)
        {
            bool isfirstparam = true;

            foreach (KeyValuePair<string, string> kvp in postdata)
            {
                if (!isfirstparam)
                {
                    writer.Write("&");
                }

                isfirstparam = false;

                writer.Write(Uri.EscapeDataString(kvp.Key));
                writer.Write("=");
                writer.Write(Uri.EscapeDataString(kvp.Value));
            }

            writer.Flush();
        }

        private static void WriteUrlEncodedPostData(TextWriter writer, object postdata)
        {
            WriteUrlEncodedPostData(
                writer, 
                TypeDescriptor.GetProperties(postdata)
                              .OfType<PropertyDescriptor>()
                              .Select(prop => new KeyValuePair<string, object>(prop.Name, prop.GetValue(postdata)))
                              .Where(prop => prop.Value != null)
                              .ToDictionary(prop => prop.Key, prop => prop.Value.ToString())
            );
        }

        public static Stream GetResponseStream(HttpWebRequest request, out HttpWebResponse response)
        {
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    response = (HttpWebResponse)ex.Response;

                }
                else
                {
                    throw;
                }
            }

            return response.GetResponseStream();
        }

        public static Stream GetResponseStream(HttpWebRequest request)
        {
            HttpWebResponse response;
            return GetResponseStream(request, out response);
        }

        public static dynamic GetResponseJson(HttpWebRequest request, out HttpWebResponse response)
        {
            return JsonHelper.ReadJson(new StreamReader(GetResponseStream(request, out response)));
        }

        public static dynamic GetResponseJson(HttpWebRequest request)
        {
            HttpWebResponse response;
            return GetResponseJson(request, out response);
        }

        public static byte[] GetResponseData(HttpWebRequest request, out HttpWebResponse response)
        {
            using (var responsestream = GetResponseStream(request, out response))
            {
                using (var memstream = new MemoryStream())
                {
                    responsestream.CopyTo(memstream);
                    return memstream.ToArray();
                }
            }
        }

        public static byte[] GetResponseData(HttpWebRequest request)
        {
            HttpWebResponse response;
            return GetResponseData(request, out response);
        }

        public static byte[] SendMultiPartPostData(HttpWebRequest request, dynamic postdata, out HttpWebResponse response)
        {
            var boundary = "--=NextPart=--_" + Guid.NewGuid().ToString();
            request.Method = "POST";
            request.ContentType = "multipart/form-data; charset=utf-8; boundary=" + boundary;
            WriteMultiPartPostData(new StreamWriter(request.GetRequestStream(), Encoding.UTF8), postdata, boundary);
            return GetResponseData(request, out response);
        }

        public static byte[] SendMultiPartPostData(HttpWebRequest request, dynamic postdata)
        {
            HttpWebResponse response;
            return SendMultiPartPostData(request, postdata, out response);
        }

        public static byte[] SendUrlEncodedPostData(HttpWebRequest request, dynamic postdata, out HttpWebResponse response)
        {
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            var stream = new MemoryStream();
            WriteUrlEncodedPostData(new StreamWriter(stream, Encoding.UTF8), postdata);
            var streamdata = stream.ToArray();
            var streamtext = Encoding.UTF8.GetString(streamdata);
            request.GetRequestStream().Write(streamdata, 0, streamdata.Length);
            return GetResponseData(request, out response);
        }

        public static byte[] SendUrlEncodedPostData(HttpWebRequest request, dynamic postdata)
        {
            HttpWebResponse response;
            return SendUrlEncodedPostData(request, postdata, out response);
        }

        public static byte[] SendJsonPostData(HttpWebRequest request, dynamic postdata, out HttpWebResponse response)
        {
            request.Method = "POST";
            request.ContentType = "application/json";
            var stream = new MemoryStream();
            JsonHelper.WriteJson(new StreamWriter(stream, Encoding.UTF8), postdata);
            var streamdata = stream.ToArray();
            var streamtext = Encoding.UTF8.GetString(streamdata);
            request.GetRequestStream().Write(streamdata, 0, streamdata.Length);
            return GetResponseData(request, out response);
        }

        public static byte[] SendJsonPostData(HttpWebRequest request, dynamic postdata)
        {
            HttpWebResponse response;
            return SendJsonPostData(request, postdata, out response);
        }

        public static dynamic ToJson(byte[] data)
        {
            var memstream = new MemoryStream(data);

            return JsonHelper.ReadJson(new StreamReader(memstream, Encoding.UTF8));
        }

        public static HttpWebRequest CreateRequest(OAuthTicket ticket, string URL)
        {
            var req = (HttpWebRequest)HttpWebRequest.Create(URL);
            
            if (Config.WebProxyHost != null)
            {
                req.Proxy = new WebProxy(Config.WebProxyHost, Config.WebProxyPort);
            }
            else
            {
                req.Proxy = null;
            }
            
            req.UserAgent = Config.CloudPrintUserAgent;
            req.Headers.Add("X-CloudPrint-Proxy", Config.CloudPrintProxyName);
            
            if (ticket != null)
            {
                req.Headers.Add("Authorization", ticket.TokenType + " " + ticket.AccessToken);
            }

            return req;
        }

        public static HttpWebRequest CreateCloudPrintRequest(OAuthTicket ticket, string iface)
        {
            return CreateRequest(ticket, Config.CloudPrintBaseURL + "/" + iface);
        }

        public static dynamic PostCloudPrintMultiPartRequest(OAuthTicket ticket, string iface, dynamic postdata)
        {
            return HTTPHelper.ToJson(HTTPHelper.SendMultiPartPostData(CreateCloudPrintRequest(ticket, iface), postdata));
        }

        public static dynamic PostCloudPrintJsonRequest(OAuthTicket ticket, string iface, dynamic postdata)
        {
            return HTTPHelper.ToJson(HTTPHelper.SendJsonPostData(CreateCloudPrintRequest(ticket, iface), postdata));
        }

        public static dynamic PostCloudPrintUrlEncodedRequest(OAuthTicket ticket, string iface, dynamic postdata)
        {
            return HTTPHelper.ToJson(HTTPHelper.SendUrlEncodedPostData(CreateCloudPrintRequest(ticket, iface), postdata));
        }
    }
}
