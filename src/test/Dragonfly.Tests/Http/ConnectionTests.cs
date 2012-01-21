using Xunit;

namespace Dragonfly.Tests.Http
{
    public class ConnectionTests : ConnectionTestsBase
    {
        [Fact(Skip = "Connection class isn't testable yet")]
        public void ConnectionClassCanBeTested()
        {
            Connection.Execute();

            Assert.Equal(true, Socket.Paused);
            Socket.Add(
@"GET / HTTP/1.1
Host: localhost
Connection: close

");
            DisconnectedEvent.WaitOne();
            Assert.Equal("HTTP/1.1 200 OK\r\n\r\n", Socket.Output);
        }
    }
}
