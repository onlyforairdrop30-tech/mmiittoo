using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ProjectInfinity.Networking
{
    /// <summary>
    /// Transport to the authoritative server. This is phase 6 groundwork: the offline prototype does
    /// not need it, but the message contract, auth handshake and correction handling are implemented
    /// so the client can be switched to server authority without reshaping gameplay code.
    ///
    /// Development uses <c>ws://</c> (plain). Production must use <c>wss://</c>. On iOS/Android the
    /// IL2CPP WebSocket client requires an internet entitlement (already set in ProjectConfigurator).
    /// </summary>
    public class NetClient : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "ws://127.0.0.1:8787";
        [SerializeField] private bool autoConnect;

        public bool IsConnected => _socket != null && _socket.State == WebSocketState.Open;

        public event Action<HelloMessage> HelloReceived;
        public event Action<AuthOkMessage> Authenticated;
        public event Action<CorrectionMessage> Corrected;
        public event Action<StateMessage> StateReceived;
        public event Action<DamageMessage> DamageReceived;
        public event Action<KillMessage> KillReceived;
        public event Action<string> ServerError;
        public event Action Disconnected;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cancellation;
        private Task _receiveLoop;
        private int _sequence;

        private async void Start()
        {
            if (autoConnect) await ConnectAsync();
        }

        private void OnDestroy() => Disconnect();

        public async Task<bool> ConnectAsync()
        {
            if (IsConnected) return true;

            try
            {
                _cancellation = new CancellationTokenSource();
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(new Uri(serverUrl), _cancellation.Token);
                _receiveLoop = ReceiveLoop(_cancellation.Token);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Net] connect failed: {exception.Message}");
                _socket = null;
                return false;
            }
        }

        public Task<bool> AuthenticateAsync(string token) => SendJson(new AuthRequest { token = token });

        public Task<bool> SendMove(Vector3 position) => SendJson(new MoveMessage
        {
            x = position.x,
            y = position.y,
            z = position.z,
            seq = ++_sequence
        });

        public Task<bool> SendAttack(string targetId, string abilityId) => SendJson(new AttackMessage
        {
            targetId = targetId,
            abilityId = abilityId,
            seq = ++_sequence
        });

        public Task<bool> Ping() => SendJson(new PingMessage { t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

        private async Task<bool> SendJson(object message)
        {
            if (!IsConnected) return false;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text,
                                        true, _cancellation.Token);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Net] send failed: {exception.Message}");
                return false;
            }
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[16 * 1024];

            try
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    var builder = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) break;
                    Dispatch(builder.ToString());
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Net] receive failed: {exception.Message}");
            }
            finally
            {
                Disconnected?.Invoke();
            }
        }

        private void Dispatch(string json)
        {
            string type = ReadType(json);

            switch (type)
            {
                case "hello": HelloReceived?.Invoke(JsonUtility.FromJson<HelloMessage>(json)); break;
                case "auth_ok": Authenticated?.Invoke(JsonUtility.FromJson<AuthOkMessage>(json)); break;
                case "auth_error":
                case "denied":
                case "error":
                {
                    var error = JsonUtility.FromJson<AuthErrorMessage>(json);
                    ServerError?.Invoke(error.reason ?? type);
                    break;
                }
                case "correction": Corrected?.Invoke(JsonUtility.FromJson<CorrectionMessage>(json)); break;
                case "state": StateReceived?.Invoke(JsonUtility.FromJson<StateMessage>(json)); break;
                case "damage": DamageReceived?.Invoke(JsonUtility.FromJson<DamageMessage>(json)); break;
                case "kill": KillReceived?.Invoke(JsonUtility.FromJson<KillMessage>(json)); break;
            }
        }

        /// <summary>Cheap type read so we can pick a concrete DTO without a JSON DOM.</summary>
        private static string ReadType(string json)
        {
            const string key = "\"type\":\"";
            int start = json.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return string.Empty;

            start += key.Length;
            int end = json.IndexOf('"', start);
            return end > start ? json.Substring(start, end - start) : string.Empty;
        }

        public void Disconnect()
        {
            try
            {
                _cancellation?.Cancel();
                if (_socket != null && _socket.State == WebSocketState.Open)
                {
                    _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                           .GetAwaiter().GetResult();
                }
                _socket?.Dispose();
            }
            catch
            {
                // Shutdown must never throw into gameplay.
            }
            finally
            {
                _socket = null;
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }
    }
}
