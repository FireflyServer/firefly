using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Firefly.Http;
using Firefly.Tests.Fakes;
using Firefly.Utils;
using Shouldly;
using Xunit;

namespace Firefly.Tests
{
    public class NewTests
    {
        readonly FakeSocket _socket;
        readonly IFireflyService _services;
        Connection _connection;

        bool _disconnected;
        Func<IDictionary<string, object>, Task> _app;

        public NewTests()
        {
            _app = env => TaskHelpers.Completed();
            _socket = new FakeSocket();
            _services = new FakeServices();
            _connection = new Connection(_services, Invoke, _socket, Disconnect);
            _connection.Execute();
        }

        Task Invoke(IDictionary<string, object> env)
        {
            return _app(env);
        }

        void Disconnect(ISocket socket)
        {
            socket.ShouldBe(_socket);
            _disconnected = true;
        }


        [Fact]
        public Task Something()
        {
            return _socket.AddAsync("GET / HTTP/1.0\r\n\r\n")
                .Then(() => _socket.Output.ShouldContain("200"));
        }
    }
}
