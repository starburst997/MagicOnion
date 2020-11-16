using ChatApp.Shared.Hubs;
using ChatApp.Shared.MessagePackObjects;
using MagicOnion.Server;
using MagicOnion.Server.Hubs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using MagicOnion.Server.OpenTelemetry;

namespace ChatApp.Server
{
    /// <summary>
    /// Chat server processing.
    /// One class instance for one connection.
    /// </summary>
    public class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
    {
        private IGroup room;
        private string myName;
        private ActivitySource activitySource;

        public ChatHub(ActivitySource activitySource)
        {
            this.activitySource = activitySource;
        }

        public async Task JoinAsync(JoinRequest request)
        {
            this.room = await this.Group.AddAsync(request.RoomName);
            this.myName = request.UserName;

            this.Broadcast(this.room).OnJoin(request.UserName);

            // dummy external operation db.
            using (var activity = activitySource.StartActivity("db:room/insert", ActivityKind.Internal))
            {
                // this is sample. use orm or any safe way.
                activity.SetTag("table", "rooms");
                activity.SetTag("query", $"INSERT INTO rooms VALUES (0, '@room', '@username', '1');");
                activity.SetTag("parameter.room", request.RoomName);
                activity.SetTag("parameter.username", request.UserName);
                await Task.Delay(TimeSpan.FromMilliseconds(2));
            }

            // use hub trace context to set your span on same level. Otherwise parent will automatically set.
            var hubTraceContext = this.Context.GetTraceContext();
            using (var activity = activitySource.StartActivity("ChatHub:hub_context_relation", ActivityKind.Internal, hubTraceContext))
            {
                // this is sample. use orm or any safe way.
                activity.SetTag("message", "this span has no relationship with this method but relate with hub context. This means no relation with parent method.");
            }
        }

        public async Task LeaveAsync()
        {
            await this.room.RemoveAsync(this.Context);

            this.Broadcast(this.room).OnLeave(this.myName);

            // dummy external operation.
            using (var activity = activitySource.StartActivity("db:room/update", ActivityKind.Internal))
            {
                // this is sample. use orm or any safe way.
                activity.SetTag("table", "rooms");
                activity.SetTag("query", $"UPDATE rooms SET status=0 WHERE id='room' AND name='@username';");
                activity.SetTag("parameter.room", this.room.GroupName);
                activity.SetTag("parameter.username", this.myName);
                await Task.Delay(TimeSpan.FromMilliseconds(2));
            }
        }

        public async Task SendMessageAsync(string message)
        {
            var response = new MessageResponse { UserName = this.myName, Message = message };
            this.Broadcast(this.room).OnSendMessage(response);

            // dummy external operation.
            using (var activity = activitySource.StartActivity($"redis:chat_latest_message", ActivityKind.Internal))
            {
                activity.SetTag("command", "set");
                activity.SetTag("parameter.key", room.GroupName);
                activity.SetTag("parameter.value", $"{myName}={message}");
                await Task.Delay(TimeSpan.FromMilliseconds(1));
            }

            await Task.CompletedTask;
        }

        public async Task GenerateException(string message)
        {
            var ex = new Exception(message);

            // dummy external operation.
            using (var activity = activitySource.StartActivity("db:errors/insert", ActivityKind.Internal))
            {
                // this is sample. use orm or any safe way.
                activity.SetTag("table", "errors");
                activity.SetTag("query", $"INSERT INTO rooms VALUES ('{ex.Message}', '{ex.StackTrace}');");
                await Task.Delay(TimeSpan.FromMilliseconds(2));
            }
            throw ex;
        }

        // It is not called because it is a method as a sample of arguments.
        public Task SampleMethod(List<int> sampleList, Dictionary<int, string> sampleDictionary)
        {
            throw new System.NotImplementedException();
        }

        protected override ValueTask OnConnecting()
        {
            // handle connection if needed.
            Console.WriteLine($"client connected {this.Context.ContextId}");
            return CompletedTask;
        }

        protected override ValueTask OnDisconnected()
        {
            // handle disconnection if needed.
            // on disconnecting, if automatically removed this connection from group.
            return CompletedTask;
        }
    }
}
