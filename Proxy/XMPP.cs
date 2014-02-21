using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using TSVCEO.CloudPrint.Util;

namespace TSVCEO.CloudPrint.Proxy
{
    public class XMPP : IDisposable
    {
        #region classes

        protected class UniqueId
        {
            private int id;

            public UniqueId()
            {
                id = 1;
            }

            public int NewId()
            {
                return Interlocked.Increment(ref id);
            }
        }

        protected class LoggedStream : Stream
        {
            protected Stream stream;

            public struct StreamPacket
            {
                public bool IsOutput;
                public bool IsError;
                public byte[] Data;
                public Exception Exception;

                public override string ToString()
                {
                    bool isoutput = IsOutput;
                    bool iserror = IsError;

                    if (!IsError)
                    {
                        List<string> lines = Encoding.UTF8.GetString(Data).Split('\n').Select(s => s.Replace("\r", "")).ToList();
                        bool haseol = false;

                        if (lines.Count > 1 && lines[lines.Count - 1] == "")
                        {
                            lines.RemoveAt(lines.Count - 1);
                        }

                        return String.Join("<EOL>\r\n", lines.Select((line) => String.Format("XMPP {0} [{1}]", isoutput ? ">" : "<", line))) + (haseol ? "<EOL>" : "") + "\r\n";
                    }
                    else
                    {
                        return String.Format("XMPP {0} !![\r\n{1}\r\n]!!\r\n", isoutput ? ">" : "<", String.Join("\r\n", Exception.Message.Split('\n').Select(s => "    " + s.Replace("\r", ""))));
                    }
                }
            }

            public static List<StreamPacket> Packets = new List<StreamPacket>();

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Logged)
                {
                    LogOutput(buffer, offset, count);
                }

                try
                {
                    stream.Write(buffer, offset, count);
                }
                catch (Exception ex)
                {
                    LogOutput(ex);
                    throw;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int len;

                try
                {
                    len = stream.Read(buffer, offset, count);
                }
                catch (Exception ex)
                {
                    LogInput(ex);
                    throw;
                }

                if (Logged)
                {
                    LogInput(buffer, offset, len);
                }

                return len;
            }

            public override bool CanRead { get { return stream.CanRead; } }
            public override bool CanSeek { get { return stream.CanSeek; } }
            public override bool CanTimeout { get { return stream.CanTimeout; } }
            public override bool CanWrite { get { return stream.CanWrite; } }
            public override long Position { get { return stream.Position; } set { stream.Position = value; } }
            public override long Length { get { return stream.Length; } }
            public override void SetLength(long value) { stream.SetLength(value); }
            public override long Seek(long offset, SeekOrigin origin) { return stream.Seek(offset, origin); }
            public override void Flush() { stream.Flush(); }
            public override int ReadTimeout { get { return stream.ReadTimeout; } set { stream.ReadTimeout = value; } }
            public override int WriteTimeout { get { return stream.WriteTimeout; } set { stream.WriteTimeout = value; } }
            public bool Logged { get; set; }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    stream.Dispose();
                }

                base.Dispose(disposing);
            }


            public LoggedStream(Stream stream)
            {
                this.stream = stream;
            }

            public static void LogPacket(byte[] data, int offset, int count, bool isoutput)
            {
                byte[] buf = new byte[count];
                Array.Copy(data, offset, buf, 0, count);
                StreamPacket packet = new StreamPacket { IsOutput = isoutput, Data = buf };
                Logger.Log(LogLevel.Debug, packet.ToString());
                Packets.Add(packet);
            }

            public static void LogPacket(Exception ex, bool isoutput)
            {
                StreamPacket packet = new StreamPacket { IsOutput = isoutput, IsError = true, Exception = ex };
                Logger.Log(LogLevel.Debug, packet.ToString());
                Packets.Add(packet);
            }

            public static void LogInput(byte[] data, int offset, int count)
            {
                LogPacket(data, offset, count, false);
            }

            public static void LogInput(Exception ex)
            {
                LogPacket(ex, false);
            }

            public static void LogOutput(byte[] data, int offset, int count)
            {
                LogPacket(data, offset, count, true);
            }

            public static void LogOutput(Exception ex)
            {
                LogPacket(ex, true);
            }
        }

        protected class XmlStream : IDisposable
        {
            public class DisposableXElement : IDisposable
            {
                private XmlStream Stream;

                public DisposableXElement(XmlStream stream, XElement element)
                {
                    Stream = stream;
                    Stream.Writer.WriteStartElement(element.Name.LocalName, element.Name.NamespaceName);
                    var rdr = element.CreateReader();
                    rdr.Read();
                    Stream.Writer.WriteAttributes(rdr, false);
                    foreach (XNode node in element.Nodes())
                    {
                        Stream.Write(node);
                    }
                    Stream.Writer.WriteWhitespace("");
                    Stream.Writer.Flush();
                }

                private void Dispose(bool disposing)
                {
                    if (disposing)
                    {
                        Stream.Writer.WriteEndElement();
                    }
                }

                public void Dispose()
                {
                    Dispose(true);
                }
            }

            private Lazy<XmlReader> _Reader;
            private Lazy<XmlWriter> _Writer;

            public XmlReader Reader
            {
                get
                {
                    return _Reader.Value;
                }
            }

            public XmlWriter Writer
            {
                get
                {
                    return _Writer.Value;
                }
            }

            private Stream _Stream { get; set; }
            public LoggedStream Stream { get; private set; }

            public XmlStream(Stream stream)
            {
                Stream = new LoggedStream(stream);
                Stream.Logged = true;
                Stream.ReadTimeout = Timeout.Infinite;
                Stream.WriteTimeout = Timeout.Infinite;

                _Reader = new Lazy<XmlReader>(() =>
                    XmlTextReader.Create(
                        Stream,
                        new XmlReaderSettings
                        {
                            CloseInput = false
                        }
                    )
                );

                _Writer = new Lazy<XmlWriter>(() =>
                    XmlWriter.Create(
                        Stream,
                        new XmlWriterSettings
                        {
                            ConformanceLevel = System.Xml.ConformanceLevel.Fragment,
                            OmitXmlDeclaration = true,
                            CloseOutput = false
                        }
                    )
                );
            }

            protected void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_Reader != null && _Reader.IsValueCreated)
                    {
                        _Reader.Value.Close();
                        _Reader = null;
                    }

                    if (_Writer != null && _Writer.IsValueCreated)
                    {
                        _Writer.Value.Close();
                        _Writer = null;
                    }
                }
            }

            public void Dispose()
            {
                Dispose(true);
            }

            public void Write(XNode element)
            {
                element.WriteTo(Writer);
                Writer.Flush();
            }

            public DisposableXElement WriteStart(XElement element)
            {
                return new DisposableXElement(this, element);
            }

            public void Write(XElement element, Action<XmlStream> callback)
            {
                using (var xel = WriteStart(element))
                {
                    callback(this);
                }
            }

            public void Expect(Func<XmlReader, bool> expect, Func<XmlReader, string> fail)
            {
                if (!expect(Reader))
                {
                    throw new XmlException("Expectation failed: " + fail(Reader));
                }
            }

            public void ReadStartElement(XName name, bool alreadyread)
            {
                if (!alreadyread)
                {
                    ReadStartElement();
                }

                XName ename = GetXName(Reader);

                if (ename != name)
                {
                    throw new XmlException(String.Format("Expected {0}; got {1}", name, ename));
                }
            }

            public void ReadStartElement(XName name)
            {
                ReadStartElement(name, false);
            }

            public void ReadStartElement()
            {
                ReadStartElement(false);
            }

            public bool ReadStartElement(bool whitespaceping)
            {
                while (Reader.Read())
                {
                    switch (Reader.NodeType)
                    {
                        case XmlNodeType.Whitespace:
                            break;
                        case XmlNodeType.Comment:
                            break;
                        case XmlNodeType.Entity:
                            break;
                        case XmlNodeType.Notation:
                            break;
                        case XmlNodeType.ProcessingInstruction:
                            break;
                        case XmlNodeType.XmlDeclaration:
                            break;
                        case XmlNodeType.Element:
                            return true;
                        default:
                            throw new XmlException(String.Format("Expected element; got {0}", Reader.NodeType));
                    }

                    if (whitespaceping) return false;
                }

                return false;
            }

            public XElement ReadElement(XName name, bool alreadyread)
            {
                ReadStartElement(name, alreadyread);
                return GetElement(Reader);
            }

            public XElement ReadElement(XName name)
            {
                return ReadElement(name, false);
            }

            public XElement ReadElement(bool alreadyread)
            {
                if (!alreadyread)
                {
                    ReadStartElement();
                }
                return GetElement(Reader);
            }

            public XElement ReadElement()
            {
                return ReadElement(false);
            }

            private XName GetXName(XmlReader reader)
            {
                XNamespace ns;
                if (reader.Prefix == "xml")
                {
                    ns = XNamespace.Xml;
                }
                else if (reader.NamespaceURI == null)
                {
                    ns = XNamespace.None;
                }
                else
                {
                    ns = reader.NamespaceURI;
                }
                return ns + reader.LocalName;
            }

            public XElement GetElement(XmlReader reader)
            {
                XElement el = new XElement(GetXName(reader));

                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        if (reader.LocalName != "xmlns" && reader.Prefix != "xmlns")
                        {
                            el.Add(new XAttribute(GetXName(reader), reader.Value));
                        }
                    }
                    while (reader.MoveToNextAttribute());
                }

                reader.MoveToElement();

                if (!reader.IsEmptyElement)
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            el.Add(GetElement(reader));
                        }
                        else if (reader.NodeType == XmlNodeType.Text)
                        {
                            el.Add(new XText(reader.Value));
                        }
                        else if (reader.NodeType == XmlNodeType.CDATA)
                        {
                            el.Add(new XCData(reader.Value));
                        }
                        else if (reader.NodeType == XmlNodeType.EndElement)
                        {
                            break;
                        }
                    }
                }

                return el;
            }
        }

        public class InfoQuery
        {
            public string Type;
            public string From;
            public string To;
            public IEnumerable<XElement> Content;
            public Action<InfoQueryResponse, CancellationToken> Callback;
        }

        public class InfoQueryResponse
        {
            public InfoQuery Query;
            public string Type;
            public string From;
            public string To;
            public IEnumerable<XElement> Content;
            public IEnumerable<XElement> Error;
            public XElement IQElement;
        }

        public class Message
        {
            public string From;
            public string To;
            public string Type;
            public string Id;
            public IEnumerable<XElement> Content;
            public IEnumerable<XElement> Error;
            public XElement MessageElement;
        }

        #endregion

        #region constants
        protected static readonly XNamespace NamespaceClient = "jabber:client";
        protected static readonly XNamespace NamespaceStream = "http://etherx.jabber.org/streams";
        protected static readonly XNamespace NamespaceStartTLS = "urn:ietf:params:xml:ns:xmpp-tls";
        protected static readonly XNamespace NamespaceSASL = "urn:ietf:params:xml:ns:xmpp-sasl";
        protected static readonly XNamespace NamespaceAuth = "http://www.google.com/talk/protocol/auth";
        protected static readonly XNamespace NamespaceBind = "urn:ietf:params:xml:ns:xmpp-bind";
        protected static readonly XNamespace NamespaceSession = "urn:ietf:params:xml:ns:xmpp-session";
        protected static readonly XNamespace NamespacePush = "google:push";
        #endregion

        #region private / protected fields / properties
        protected UniqueId InfoQueryID { get; set; }

        protected string LoginJID { get; set; }
        protected string ProxyHost { get; set; }
        protected int ProxyPort { get; set; }
        protected string EndpointHost { get; set; }
        protected int EndpointPort { get; set; }
        protected string AuthCookie { get; set; }
        protected string AuthMechanism { get; set; }
        protected string ResourceName { get; set; }
        protected string FullJID { get; set; }
        protected bool IsReadyForSubscriptions { get; set; }
        protected Dictionary<string, Action<XElement, XMPP>> Subscriptions { get; set; }
        protected AutoResetEvent QueriesQueued { get; set; }
        protected AutoResetEvent ResponsesQueued { get; set; }
        protected AutoResetEvent MessagesQueued { get; set; }
        protected AutoResetEvent SubscriptionsQueued { get; set; }
        protected ManualResetEvent ReaderFaulted { get; set; }
        protected Exception ReaderException { get; set; }
        protected Exception WriterException { get; set; }
        protected ConcurrentQueue<InfoQuery> QueuedQueries { get; set; }
        protected ConcurrentDictionary<string, InfoQuery> RunningQueries { get; set; }
        protected ConcurrentQueue<InfoQueryResponse> QueuedResponses { get; set; }
        protected ConcurrentQueue<Message> QueuedMessages { get; set; }
        protected ConcurrentQueue<Func<InfoQuery>> QueuedSubscriptions { get; set; }
        protected CancellationTokenSource CancelSource { get; set; }
        protected Thread ReaderThread { get; set; }
        protected Thread WriterThread { get; set; }

        protected string BareJID { get { return FullJID.Substring(0, FullJID.IndexOf('/')); } }
        protected string Domain { get { return LoginJID.Substring(LoginJID.IndexOf('@') + 1); } }
        #endregion

        #region public properties / events
        public bool IsSubscribed { get { return Subscriptions.Count != 0; } }
        public bool IsCancelled { get { return CancelSource.IsCancellationRequested; } }
        #endregion

        #region constructors / destructors
        public XMPP(string jid, string authCookie, string resourceName, string authMechanism = "PLAIN", string proxyHost = null, int proxyPort = 0, string host = null, int port = 0)
        {
            this.ResourceName = resourceName;
            this.ProxyHost = proxyHost;
            this.ProxyPort = proxyPort;
            this.EndpointHost = host ?? "talk.google.com";
            this.EndpointPort = port != 0 ? port : 5222;
            this.LoginJID = jid;
            this.AuthCookie = authCookie;
            this.AuthMechanism = authMechanism;
            this.InfoQueryID = new UniqueId();
            this.Subscriptions = new Dictionary<string, Action<XElement, XMPP>>();
            this.QueuedQueries = new ConcurrentQueue<InfoQuery>();
            this.QueuedSubscriptions = new ConcurrentQueue<Func<InfoQuery>>();
            this.QueuedResponses = new ConcurrentQueue<InfoQueryResponse>();
            this.QueuedMessages = new ConcurrentQueue<Message>();
            this.QueriesQueued = new AutoResetEvent(false);
            this.SubscriptionsQueued = new AutoResetEvent(false);
            this.ResponsesQueued = new AutoResetEvent(false);
            this.MessagesQueued = new AutoResetEvent(false);
            this.ReaderFaulted = new ManualResetEvent(false);
            this.RunningQueries = new ConcurrentDictionary<string, InfoQuery>();
            this.CancelSource = new CancellationTokenSource();
        }

        ~XMPP()
        {
            Dispose(false);
        }
        #endregion

        #region private / protected methods
        protected void Dispose(bool disposing)
        {
            Stop(false);

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        protected void EnqueueInfoQuery(string type, string from, string to, Action<InfoQueryResponse, CancellationToken> callback, params XElement[] content)
        {
            EnqueueInfoQuery(type, from, to, callback, (IEnumerable<XElement>)content);
        }

        protected void EnqueueInfoQuery(string type, string from, string to, Action<InfoQueryResponse, CancellationToken> callback, IEnumerable<XElement> content)
        {
            QueuedQueries.Enqueue(new InfoQuery { Type = type, From = from, To = to, Content = content, Callback = callback });
            QueriesQueued.Set();
        }

        protected void EnqueueSubscription(Action<InfoQueryResponse, CancellationToken> callback, params XElement[] content)
        {
            EnqueueSubscription(callback, (IEnumerable<XElement>)content);
        }

        protected void EnqueueSubscription(Action<InfoQueryResponse, CancellationToken> callback, IEnumerable<XElement> content)
        {
            QueuedSubscriptions.Enqueue(() => new InfoQuery { Type = "set", From = null, To = BareJID, Content = content, Callback = callback });
            SubscriptionsQueued.Set();
        }

        protected void EnqueueInfoQueryResponse(XElement iq)
        {
            InfoQuery query = RunningQueries[iq.Attribute("id").Value];
            string type = iq.Attribute("type").Value;
            XAttribute from = iq.Attribute("from");
            XAttribute to = iq.Attribute("to");
            IEnumerable<XElement> error = iq.Elements(NamespaceClient + "error");
            IEnumerable<XElement> content = iq.Elements().Where(el => el.Name != (NamespaceClient + "error"));

            QueuedResponses.Enqueue(
                new InfoQueryResponse
                {
                    Query = query,
                    Type = type,
                    From = from == null ? null : from.Value,
                    To = to == null ? null : to.Value,
                    Content = content,
                    Error = error,
                    IQElement = iq
                }
            );

            ResponsesQueued.Set();
        }

        protected void EnqueueMessage(XElement msg)
        {
            XAttribute from = msg.Attribute("from");
            XAttribute to = msg.Attribute("to");
            XAttribute type = msg.Attribute("type");
            XAttribute id = msg.Attribute("id");
            IEnumerable<XElement> error = msg.Elements(NamespaceClient + "error");
            IEnumerable<XElement> content = msg.Elements().Where(el => el.Name != (NamespaceClient + "error"));

            QueuedMessages.Enqueue(
                new Message
                {
                    From = from == null ? null : from.Value,
                    To = to == null ? null : to.Value,
                    Type = type == null ? "normal" : type.Value,
                    Id = id == null ? null : id.Value,
                    Content = content,
                    Error = error,
                    MessageElement = msg
                }
            );

            if (MessagesQueued != null)
            {
                MessagesQueued.Set();
            }
        }

        protected void XMPPReader(XmlStream xml, CancellationToken canceltoken)
        {
            try
            {
                while (!canceltoken.IsCancellationRequested)
                {
                    if (xml.ReadStartElement(true))
                    {
                        XElement el = xml.ReadElement(true);

                        if (!canceltoken.IsCancellationRequested)
                        {
                            if (el.Name == (NamespaceClient + "iq"))
                            {
                                EnqueueInfoQueryResponse(el);
                            }
                            else if (el.Name == (NamespaceClient + "message"))
                            {
                                EnqueueMessage(el);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, "XMPP Reader task caught exception {0}\n{1}", ex.Message, ex.ToString());
                ReaderException = ex;
                ReaderFaulted.Set();
            }
        }

        protected void ProcessQueuedQueries(XmlStream xml, CancellationToken canceltoken)
        {
            InfoQuery iq;

            if (IsReadyForSubscriptions)
            {
                Func<InfoQuery> iqfactory;
                while (!canceltoken.IsCancellationRequested && QueuedSubscriptions.TryDequeue(out iqfactory))
                {
                    ProcessInfoQuery(xml, iqfactory());
                }
            }

            while (!canceltoken.IsCancellationRequested && QueuedQueries.TryDequeue(out iq))
            {
                ProcessInfoQuery(xml, iq);
            }

            canceltoken.ThrowIfCancellationRequested();
        }

        protected void ProcessInfoQuery(XmlStream xml, InfoQuery iq)
        {
            int id = InfoQueryID.NewId();
            xml.Write(
                new XElement(NamespaceClient + "iq",
                    new XAttribute("type", iq.Type),
                    iq.To == null ? null : new XAttribute("to", iq.To),
                    new XAttribute("id", id),
                    iq.Content
                )
            );

            RunningQueries[id.ToString()] = iq;
        }

        protected void ProcessQueuedResponses(XmlStream xml, CancellationToken canceltoken)
        {
            InfoQueryResponse iq;
            while (!canceltoken.IsCancellationRequested && QueuedResponses.TryDequeue(out iq))
            {
                iq.Query.Callback(iq, canceltoken);
            }

            canceltoken.ThrowIfCancellationRequested();
        }

        protected void ProcessQueuedMessages(XmlStream xml, CancellationToken canceltoken)
        {
            Message msg;
            while (!canceltoken.IsCancellationRequested && QueuedMessages.TryDequeue(out msg))
            {
                foreach (XElement el in msg.Content)
                {
                    if (el.Name.ToString() == "{google:push}push")
                    {
                        string channel = el.Attribute("channel").Value;
                        if (Subscriptions.ContainsKey(channel))
                        {
                            Logger.Log(LogLevel.Debug, "Handling message for channel {0}", channel);
                            Subscriptions[channel](el, this);
                        }
                        else
                        {
                            Logger.Log(LogLevel.Debug, "Message for unknown channel {0}", channel);
                        }
                    }
                }
            }

            canceltoken.ThrowIfCancellationRequested();
        }

        protected void BeginSession()
        {
            EnqueueInfoQuery(
                "set", 
                null, 
                null,
                (iq, canceltoken) => {
                    if (iq.Type == "result")
                    {
                        IsReadyForSubscriptions = true;
                    }
                    else
                    {
                        throw new InvalidOperationException("Bad response");
                    }
                },
                new XElement(NamespaceSession + "session")
            );
        }
        
        protected void BeginBind()
        {
            EnqueueInfoQuery(
                "set", 
                null, 
                null,
                (iq, canceltoken) => {
                    if (iq.Type == "result" && iq.Content.Single().Name == (NamespaceBind + "bind"))
                    {
                        this.FullJID = iq.Content.Single().Value;
                        BeginSession();
                    }
                    else
                    {
                        throw new InvalidOperationException("Bad Response");
                    }
                },
                new XElement(NamespaceBind + "bind",
                    new XElement(NamespaceBind + "resource",
                        new XText(ResourceName)
                    )
                )
            );
        }
        
        protected void StartXMPPStream(XmlStream xml, CancellationToken canceltoken)
        {
            using (var xmppstream = xml.WriteStart(new XElement(NamespaceStream + "stream", new XAttribute("to", Domain), new XAttribute(XNamespace.Xml + "lang", "en"), new XAttribute("version", "1.0"))))
            {
                xml.ReadStartElement(NamespaceStream + "stream");
                xml.Expect((rdr) => rdr.GetAttribute("from") == Domain, (rdr) => String.Format("Expected domain {0}; got domain {1}", Domain, rdr.GetAttribute("from")));
                var features = xml.ReadElement(NamespaceStream + "features");
                ReaderThread = new Thread(new ThreadStart(() => XMPPReader(xml, canceltoken)));
                ReaderThread.Start();
                BeginBind();

                while (!canceltoken.IsCancellationRequested)
                {
                    switch (WaitHandle.WaitAny(new WaitHandle[] { canceltoken.WaitHandle, ReaderFaulted, QueriesQueued, IsReadyForSubscriptions ? SubscriptionsQueued : QueriesQueued, ResponsesQueued, MessagesQueued }, 15000))
                    {
                        case 0:
                            break;
                        case 1:
                            throw new AggregateException(ReaderException);
                        case 2:
                            ProcessQueuedQueries(xml, canceltoken);
                            break;
                        case 3:
                            goto case 2;
                        case 4:
                            ProcessQueuedResponses(xml, canceltoken);
                            break;
                        case 5:
                            ProcessQueuedMessages(xml, canceltoken);
                            break;
                        case WaitHandle.WaitTimeout:
                            xml.Write(new XText(" "));
                            break;
                    }
                }

                throw new OperationCanceledException();
            }
        }
        
        protected void StartTLSStream(XmlStream xml, CancellationToken canceltoken)
        {
            using (var xmppstream = xml.WriteStart(new XElement(NamespaceStream + "stream", new XAttribute("to", Domain), new XAttribute(XNamespace.Xml + "lang", "en"), new XAttribute("version", "1.0"))))
            {
                xml.ReadStartElement(NamespaceStream + "stream");
                xml.Expect((rdr) => rdr.GetAttribute("from") == Domain, (rdr) => String.Format("Expected domain {0}; got domain {1}", Domain, rdr.GetAttribute("from")));
                var features = xml.ReadElement(NamespaceStream + "features");
                xml.Write(
                    new XElement(NamespaceSASL + "auth",
                        new XAttribute("mechanism", AuthMechanism),
                        new XAttribute(NamespaceAuth + "service", "chromiumsync"),
                        new XAttribute(NamespaceAuth + "allow-generated-jid", "true"),
                        new XAttribute(NamespaceAuth + "client-uses-full-bind-result", "true"),
                        new XText(Convert.ToBase64String(Encoding.ASCII.GetBytes("\0" + LoginJID + "\0" + AuthCookie)))
                    )
                );
                xml.ReadStartElement(NamespaceSASL + "success");
                xml.Stream.Logged = false;
                StartXMPPStream(new XmlStream(xml.Stream), canceltoken);
            }
        }

        protected static Stream Connect(string Host, int Port, string ProxyHost = null, int ProxyPort = 0)
        {
            if (ProxyHost != null && ProxyPort != 0)
            {
                var client = new TcpClient();
                client.Connect(ProxyHost, ProxyPort);
                var stream = client.GetStream();
                var requestbytes = Encoding.ASCII.GetBytes(String.Format("CONNECT {0}:{1} HTTP/1.1\r\nHost: {0}:{1}\r\nProxy-Connection: keep-alive\r\n\r\n", Host, Port));
                LoggedStream.LogOutput(requestbytes, 0, requestbytes.Length);
                stream.Write(requestbytes, 0, requestbytes.Length);
                var responsebytes = new byte[65536];
                var pos = 0;
                var len = 0;

                while ((len = stream.Read(responsebytes, pos, responsebytes.Length - pos)) != 0)
                {
                    pos += len;
                    LoggedStream.LogInput(responsebytes, 0, pos);
                    var response = Encoding.ASCII.GetString(responsebytes, 0, pos).Replace("\r", "").Split('\n').ToArray();

                    if (response.Length < 2) continue;
                    if (response[response.Length - 1] != "") continue;

                    var responsestatus = response[0].Split(new char[] { ' ' }, 3);

                    if (responsestatus.Length < 2) throw new WebException("Invalid Response Status Line " + response[0], WebExceptionStatus.ProtocolError);
                    if (!responsestatus[0].StartsWith("HTTP")) throw new WebException("Invalid Protocol " + responsestatus[0], WebExceptionStatus.ProtocolError);
                    if (responsestatus[1] != "200") throw new WebException(responsestatus[2], WebExceptionStatus.ConnectionClosed);

                    return stream;
                }

                throw new WebException(String.Format("Connection Closed Prematurely after reading {0} bytes", pos), WebExceptionStatus.ConnectionClosed);
            }
            else
            {
                return new TcpClient(Host, Port).GetStream();
            }
        }

        protected void StartPlaintextStream(CancellationToken canceltoken)
        {
            using (var stream = Connect(EndpointHost, EndpointPort, ProxyHost, ProxyPort))
            {
                using (var xml = new XmlStream(stream))
                {
                    using (var xmppstream = xml.WriteStart(new XElement(NamespaceStream + "stream", new XAttribute("to", Domain), new XAttribute(XNamespace.Xml + "lang", "en"), new XAttribute("version", "1.0"))))
                    {
                        xml.ReadStartElement(NamespaceStream + "stream");
                        xml.Expect((rdr) => rdr.GetAttribute("from") == Domain, (rdr) => String.Format("Expected domain {0}; got domain {1}", Domain, rdr.GetAttribute("from")));
                        var features = xml.ReadElement(NamespaceStream + "features");
                        xml.Write(new XElement(NamespaceStartTLS + "starttls"));
                        xml.ReadStartElement(NamespaceStartTLS + "proceed");
                        xml.Stream.Logged = false;
                        using (var ssl = new SslStream(xml.Stream, true, (sender, cert, chain, errs) => true))
                        {
                            ssl.AuthenticateAsClient(EndpointHost);
                            using (var sslxml = new XmlStream(ssl))
                            {
                                StartTLSStream(sslxml, canceltoken);
                            }
                        }
                    }
                }
            }
        }

        protected void StartXMPPWithThreadEndedCallback(CancellationToken canceltoken, Action<Exception, XMPP> callback)
        {
            try
            {
                StartPlaintextStream(canceltoken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, "XMPP Writer task caught exception {0}\n{1}", ex.Message, ex.ToString());
                WriterException = ex;
                callback(ex, this);
            }
        }

        protected Thread StartXMPPThread(Action<Exception, XMPP> callback)
        {
            CancellationToken canceltoken = CancelSource.Token;
            Thread thread = new Thread(new ThreadStart(() => StartXMPPWithThreadEndedCallback(canceltoken, callback)));
            thread.Start();
            return thread;
        }

        protected void Wait(bool _throw)
        {
            if (WriterThread != null)
            {
                WriterThread.Join();

                if (WriterException != null)
                {
                    Exception ex = WriterException;
                    WriterException = null;

                    if (_throw)
                    {
                        throw new AggregateException(ex);
                    }
                }
            }
        }
        #endregion

        #region public methods
        public void Dispose()
        {
            Dispose(true);
        }

        public void Subscribe(string channel, Action<XElement, XMPP> callback)
        {
            EnqueueSubscription(
                (iq, canceltoken) =>
                {
                    if (iq.Type == "result")
                    {
                        Subscriptions[channel] = callback;
                    }
                    else
                    {
                        throw new InvalidOperationException("Bad Response");
                    }
                },
                new XElement(NamespacePush + "subscribe",
                    new XElement(NamespacePush + "item",
                        new XAttribute("channel", channel),
                        new XAttribute("from", channel)
                    )
                )
            );
        }

        public void Stop(bool _throw)
        {
            CancelSource.Cancel();
            Wait(_throw);
        }

        public void Start(Action<Exception, XMPP> callback)
        {
            if (this.WriterThread == null)
            {
                this.WriterThread = StartXMPPThread(callback);
            }
        }
        #endregion
    }
}
