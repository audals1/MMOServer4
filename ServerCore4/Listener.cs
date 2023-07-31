using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace ServerCore4
{
    public class Listener
    {
        Socket _listenSocket;
        Func<Session> _sessionFactory;

        public void Init(IPEndPoint endPoint, Func<Session> sessionFactory)
        {
            _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _sessionFactory += sessionFactory;

            //문지기 교육
            _listenSocket.Bind(endPoint); //문지기에게 주소랑 포트번호를 박아넣음

            //서버 오픈

            _listenSocket.Listen(10);//backlog : 최대 대기수

            for (int i = 0; i < 10; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs(); //한번만 만들면 재사용가능
                args.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted); //pending상황이어도 이벤트 핸들러로 나중에 함수콜백함
                RegisterAccept(args); //초기화때 한번은 실행
            }
        }

        void RegisterAccept(SocketAsyncEventArgs args) // 그냥 ㄱㄱ 상황
        {
            args.AcceptSocket = null; // 여러바퀴째에는 재사용이므로 Accept Socket 초기화 해줘야 함

            //비동기로 하면 클라 접속 안해도 일단 ㄱㄱ 리턴
            //넘어갔는데 그때 클라 들어오면?? 그래서 2가지 경우로 나눠서 처리해줘야함
            bool pending = _listenSocket.AcceptAsync(args); //리턴 값이 bool
            if (pending == false) //false는 대기없이 바로 클라 접속한 상황 true면 나중에 콜백해야하는 상황
                OnAcceptCompleted(null, args); //이벤트핸들러 매개변수 형식 맞춰줌
        }

        void OnAcceptCompleted(object sender, SocketAsyncEventArgs args) //접속성공상황
        {
            if (args.SocketError == SocketError.Success)
            {
                //To Do
                Session session = _sessionFactory.Invoke();
                session.Start(args.AcceptSocket);
                session.OnConnected(args.AcceptSocket.RemoteEndPoint);
            }
            else
                Console.WriteLine(args.SocketError.ToString());

            RegisterAccept(args); //다음 클라를 위해 Accept 예약
        }
    }
}
