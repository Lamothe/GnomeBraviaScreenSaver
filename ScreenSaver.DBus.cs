namespace ScreenSaver.DBus
{
    using System;
    using Tmds.DBus.Protocol;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    partial class ScreenSaver : ScreenSaverObject
    {
        private const string __Interface = "org.gnome.ScreenSaver";
        public ScreenSaver(ScreenSaverService service, ObjectPath path) : base(service, path)
        { }
        public Task LockAsync()
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    member: "Lock");
                return writer.CreateMessage();
            }
        }
        public Task<bool> GetActiveAsync()
        {
            return this.Connection.CallMethodAsync(CreateMessage(), (Message m, object? s) => ReadMessage_b(m, (ScreenSaverObject)s!), this);
            MessageBuffer CreateMessage()
            {
                var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    member: "GetActive");
                return writer.CreateMessage();
            }
        }
        public Task SetActiveAsync(bool value)
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    signature: "b",
                    member: "SetActive");
                writer.WriteBool(value);
                return writer.CreateMessage();
            }
        }
        public Task<uint> GetActiveTimeAsync()
        {
            return this.Connection.CallMethodAsync(CreateMessage(), (Message m, object? s) => ReadMessage_u(m, (ScreenSaverObject)s!), this);
            MessageBuffer CreateMessage()
            {
                var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    member: "GetActiveTime");
                return writer.CreateMessage();
            }
        }
        public ValueTask<IDisposable> WatchActiveChangedAsync(Action<Exception?, bool> handler, bool emitOnCapturedContext = true, ObserverFlags flags = ObserverFlags.None)
            => base.WatchSignalAsync(Service.Destination, __Interface, Path, "ActiveChanged", (Message m, object? s) => ReadMessage_b(m, (ScreenSaverObject)s!), handler, emitOnCapturedContext, flags);
        public ValueTask<IDisposable> WatchWakeUpScreenAsync(Action<Exception?> handler, bool emitOnCapturedContext = true, ObserverFlags flags = ObserverFlags.None)
            => base.WatchSignalAsync(Service.Destination, __Interface, Path, "WakeUpScreen", handler, emitOnCapturedContext, flags);
    }
    partial class ScreenSaverService
    {
        public Tmds.DBus.Protocol.Connection Connection { get; }
        public string Destination { get; }
        public ScreenSaverService(Tmds.DBus.Protocol.Connection connection, string destination)
            => (Connection, Destination) = (connection, destination);
        public ScreenSaver CreateScreenSaver(ObjectPath path) => new ScreenSaver(this, path);
    }
    class ScreenSaverObject
    {
        public ScreenSaverService Service { get; }
        public ObjectPath Path { get; }
        protected Tmds.DBus.Protocol.Connection Connection => Service.Connection;
        protected ScreenSaverObject(ScreenSaverService service, ObjectPath path)
            => (Service, Path) = (service, path);
        protected MessageBuffer CreateGetPropertyMessage(string @interface, string property)
        {
            var writer = this.Connection.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: Service.Destination,
                path: Path,
                @interface: "org.freedesktop.DBus.Properties",
                signature: "ss",
                member: "Get");
            writer.WriteString(@interface);
            writer.WriteString(property);
            return writer.CreateMessage();
        }
        protected MessageBuffer CreateGetAllPropertiesMessage(string @interface)
        {
            var writer = this.Connection.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: Service.Destination,
                path: Path,
                @interface: "org.freedesktop.DBus.Properties",
                signature: "s",
                member: "GetAll");
            writer.WriteString(@interface);
            return writer.CreateMessage();
        }
        protected ValueTask<IDisposable> WatchPropertiesChangedAsync<TProperties>(string @interface, MessageValueReader<PropertyChanges<TProperties>> reader, Action<Exception?, PropertyChanges<TProperties>> handler, bool emitOnCapturedContext, ObserverFlags flags)
        {
            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Sender = Service.Destination,
                Path = Path,
                Interface = "org.freedesktop.DBus.Properties",
                Member = "PropertiesChanged",
                Arg0 = @interface
            };
            return this.Connection.AddMatchAsync(rule, reader,
                                                    (Exception? ex, PropertyChanges<TProperties> changes, object? rs, object? hs) => ((Action<Exception?, PropertyChanges<TProperties>>)hs!).Invoke(ex, changes),
                                                    this, handler, emitOnCapturedContext, flags);
        }
        public ValueTask<IDisposable> WatchSignalAsync<TArg>(string sender, string @interface, ObjectPath path, string signal, MessageValueReader<TArg> reader, Action<Exception?, TArg> handler, bool emitOnCapturedContext, ObserverFlags flags)
        {
            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Sender = sender,
                Path = path,
                Member = signal,
                Interface = @interface
            };
            return this.Connection.AddMatchAsync(rule, reader,
                                                    (Exception? ex, TArg arg, object? rs, object? hs) => ((Action<Exception?, TArg>)hs!).Invoke(ex, arg),
                                                    this, handler, emitOnCapturedContext, flags);
        }
        public ValueTask<IDisposable> WatchSignalAsync(string sender, string @interface, ObjectPath path, string signal, Action<Exception?> handler, bool emitOnCapturedContext, ObserverFlags flags)
        {
            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Sender = sender,
                Path = path,
                Member = signal,
                Interface = @interface
            };
            return this.Connection.AddMatchAsync<object>(rule, (Message message, object? state) => null!,
                                                            (Exception? ex, object v, object? rs, object? hs) => ((Action<Exception?>)hs!).Invoke(ex), this, handler, emitOnCapturedContext, flags);
        }
        protected static bool ReadMessage_b(Message message, ScreenSaverObject _)
        {
            var reader = message.GetBodyReader();
            return reader.ReadBool();
        }
        protected static uint ReadMessage_u(Message message, ScreenSaverObject _)
        {
            var reader = message.GetBodyReader();
            return reader.ReadUInt32();
        }
    }
    class PropertyChanges<TProperties>
    {
        public PropertyChanges(TProperties properties, string[] invalidated, string[] changed)
        	=> (Properties, Invalidated, Changed) = (properties, invalidated, changed);
        public TProperties Properties { get; }
        public string[] Invalidated { get; }
        public string[] Changed { get; }
        public bool HasChanged(string property) => Array.IndexOf(Changed, property) != -1;
        public bool IsInvalidated(string property) => Array.IndexOf(Invalidated, property) != -1;
    }
}
