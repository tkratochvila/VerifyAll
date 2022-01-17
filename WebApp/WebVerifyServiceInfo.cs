using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net.WebSockets;
using InterLayerLib;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;


namespace webApp
{
    public class WebVerifyServiceInfo
    {
        // TO-DO: access to different params (socket, checekr, etc. thru methods with locks)
        private Guid _guid;
        private WebSocket _socket;
        private Queue<string> _outputMessageQue = new Queue<string>();
        private bool _sendingMessageNow = false;
        private Checker _checker = null;
        private Action<CheckerMessage, Checker, WebSocket> _checkerEvent;
        private CancellationTokenSource _socketTimeoutCancellation;

        public WebVerifyServiceInfo(Guid guid, WebSocket socket, Action<CheckerMessage, Checker, WebSocket> checkerEvent)
        {
            this._guid = guid;
            this._socket = socket;
            this._checkerEvent = checkerEvent;
        }

        public bool isActive()
        {
            // TO-DO: more sofisticated check, whether is active or not...check socket, check config in session directory if it waits for something? timeout?
            return true;
        }

        public void Clean()
        {
            // TO-DO: more sophisticated
            this.socketDisconnected();
            // end of socket
            // end of checker?
            Directory.Delete(Path.Combine(Directory.GetCurrentDirectory(), guid.ToString()), true);
        }

        public void socketDisconnected()
        {
            if (_socket != null)
            {
                _socket.Dispose();
                _socket = null;
                this.stopSocketTimeout();
            }
        }

        public void restartSocketTimeout()
        {
            this.stopSocketTimeout();

            this._socketTimeoutCancellation = new CancellationTokenSource();

            Task.Delay(30000).ContinueWith(async (t) =>
            {
                // TO-DO: release socket
                if (_socket != null)
                {
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "No message in a while!", CancellationToken.None);
                }

            }, this._socketTimeoutCancellation.Token);
        }

        public void stopSocketTimeout()
        {
            if (this._socketTimeoutCancellation != null)
            {
                this._socketTimeoutCancellation.Cancel();
                this._socketTimeoutCancellation.Dispose();
            }
        }

        public Task sendWSMessage(string msg)
        {
            return Task.Run(() =>
            {
                lock (_outputMessageQue)
                {
                    _outputMessageQue.Enqueue(msg);
                }
                sendAllOutputMessagesFromQue();
            });    
        }

        public void sendWSControlMessage(string msg)
        {
            lock (_socket)
            {
                _socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private void sendAllOutputMessagesFromQue()
        {
            lock(_outputMessageQue)
            {
                if (!_sendingMessageNow)
                {
                    if (_outputMessageQue.Count > 0)
                    {
                        _sendingMessageNow = true;
                        string msg = _outputMessageQue.Peek();
                        _socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None).ContinueWith((tResult) =>
                        {
                            if(tResult.IsCanceled || tResult.IsFaulted)
                            {
                                // To-DO: somehow initiate socket close or something like that?
                                _sendingMessageNow = false;
                            }
                            else                           
                            {
                                lock (_outputMessageQue)
                                {
                                    _outputMessageQue.Dequeue();
                                }
                                _sendingMessageNow = false;
                                sendAllOutputMessagesFromQue();
                            }
                        });
                    }
                }
            }
        }

        public void handOverSocketToDifferentServiceInfo(WebVerifyServiceInfo to)
        {
                to.handOverSocketFromDifferentServiceInfo(_socket);
                _socket = null;
        }

        public void handOverSocketFromDifferentServiceInfo(WebSocket socket)
        {
            _socket = socket;
            sendAllOutputMessagesFromQue();
        }

        public Guid guid
        {
            get
            {
                return _guid;
            }

            set
            {
                _guid = value;
            }
        }

        //public WebSocket socket
        //{
        //    //get
        //    //{
        //    //    return _socket;
        //    //}

        //    set
        //    {
        //        _socket = value;
        //        sendAllOutputMessagesFromQue();
        //    }
        //}

        public Checker checker
        {
            get
            {
                if (this._checker == null)
                {
                    this._checker = new Checker(guid.ToString());
                    try
                    {
                        //checker.loadConfigs();
                        this._checker.loadAutomationServers(AppDomain.CurrentDomain.BaseDirectory + "..\\configs\\AutomationServers.xml");
                        this._checker.LoadVerificationToolCfg(AppDomain.CurrentDomain.BaseDirectory + "..\\configs\\VerificationTools.xml");
                    }
                    catch (WarningException ex)
                    {
                        // TO DO: report
                    }
                    catch (ErrorException ex)
                    {
                        // TO DO: report and close
                    }
                    this._checker.subscribeEvents((msg) => { this._checkerEvent(msg, this._checker, this._socket); });
                }

                return this._checker;
            }

            set
            {
                _checker = value;
            }
        }
    }
}