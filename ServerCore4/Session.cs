using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore4
{
    public abstract class PacketSession : Session
    {
        public static readonly int HeaderSize = 2;
        //[size(2)][packetid(2)][...][size(2)][packetid(2)][...]
        public sealed override int OnRecv(ArraySegment<byte> buffer)
        {
            //덜 왔으면 다 올때까지 기다리는 작업 ex)size 2바이트인데 1바이트만 왔으면 기달
            //최소 헤더는 파싱할 수 있는지 확인
            int processlen = 0;
            while (true)
            {
                if (buffer.Count < HeaderSize)
                    break;

                //패킷이 완전체로 도착했는지 확인
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (buffer.Count < dataSize)
                    break;

                //여기까지 왔으면 패킷 조립 가능
                OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));

                processlen += dataSize;
                //처리된 부분 뒤부터 다시 시작 되도록 버퍼 이동
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);

            }
            return processlen;
        }
        public abstract void OnRecvPacket(ArraySegment<byte> buffer);
    }
    public abstract class Session
    {
        Socket _socket;
        int _disconnected = 0;

        RecvBuffer _recvBuffer = new RecvBuffer(1024); //리시브 버퍼 생성

        object _lock = new object(); //센드버퍼 접근 제한을 위한 락

        //보낼데이터를 큐에 담아서 쌓아놓고 직전꺼 전송완료시에 하나씩 꺼내서 보냄
        Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>(); //큐생성


        //sendArgs 재사용위해 멤버변수로 올림
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();

        public abstract void OnConnected(EndPoint endPoint);
        public abstract int OnRecv(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected(EndPoint endPoint);

        public void Start(Socket socket)
        {
            _socket = socket;
            _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted); //센드이벤초기화

            RegisterRecv(); //초기화 후 한번은 낚시대 던짐
        }

        public void Send(ArraySegment<byte> segment)
        {
            //보낼때 마다 새로 이벤트를 만들어야 함 뭐 얼마나 보낼지를 모르니까 재사용이 안된다
            //SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();

            //보낼 데이터를 일단 큐에 담고 내가 1빠다 하면 그때 전송 예약한다 1빠 아니면 큐에 담아둔다
            lock (_lock)
            {
                _sendQueue.Enqueue(segment);
                if (_pendingList.Count == 0)
                    RegisterSend();
            }
        }

        public void Disconnected()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1) //이미 한번 디스커넥
                return;

            OnDisconnected(_socket.RemoteEndPoint);
            //쫓아냄
            _socket.Shutdown(SocketShutdown.Both); //듣기 말하기 둘다 안해
            _socket.Close();
        }

        #region 네트워크 통신

        void RegisterSend()
        {
            //List<ArraySegment<byte>> list = new List<ArraySegment<byte>>();//사용법을 그냥 일케 만들어놧음
            //따로 어레이 세그먼트 리스트 인스턴스 하고 버퍼들 Add한 후 그 list를 args.BufferList에 할당
            //

            while (_sendQueue.Count > 0)
            {
                ArraySegment<byte> buff = _sendQueue.Dequeue();//큐에 담아둔거 꺼냄
                //_sendArgs.SetBuffer(buff, 0, buff.Length);
                _pendingList.Add(buff);
            }
            _sendArgs.BufferList = _pendingList;

            bool pending = _socket.SendAsync(_sendArgs);
            if (pending == false)
                OnSendCompleted(null, _sendArgs);
        }

        void OnSendCompleted(object sender, SocketAsyncEventArgs args)
        {
            lock (_lock) //이 함수는 콜백형태로 호출됐을때 경합조건 발생할 수 있으므로 락 걸어줘야함
            {
                if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                {
                    try
                    {
                        _sendArgs.BufferList = null;
                        _pendingList.Clear();

                        OnSend(_sendArgs.BytesTransferred);

                        if (_sendQueue.Count > 0) //큐에 보낼 버퍼가 남았는지 체크
                        {
                            RegisterSend(); //남은거 다 털기
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"OnSendCompleted Failed {e}");
                    }
                }
                else
                {
                    Disconnected();
                }
            }
        }

        void RegisterRecv()
        {
            _recvBuffer.Clean();
            ArraySegment<byte> segment = _recvBuffer.WriteSegment; //범위 설정
            _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

            bool pending = _socket.ReceiveAsync(_recvArgs);
            if (pending == false) //리스너와 똑같음 대기 없이 바로 접속 시
                OnRecvCompleted(null, _recvArgs);//접속성공으로
        }

        void OnRecvCompleted(object sender, SocketAsyncEventArgs args)
        {
            //받은 바이트가 0보다 크다->받은게 있다 && 석세스 체크
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    //write 커서 이동
                    if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                    {
                        Disconnected();
                        return;
                    }

                    //컨텐츠 쪽으로 데이터를 넘겨주고 얼마나 처리했는지 받는다
                    int processLen = OnRecv(_recvBuffer.ReadSegment);

                    if (processLen < 0 || _recvBuffer.DataSize < processLen)
                    {
                        Disconnected();
                        return;
                    }

                    //Read 커서 이동
                    if (_recvBuffer.OnRead(processLen) == false)
                    {
                        Disconnected();
                        return;
                    }

                    RegisterRecv();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"OnRecvCompleted Failed {e}");
                }
            }
            else
            {
                Disconnected();
            }
        }
        #endregion
    }
}
