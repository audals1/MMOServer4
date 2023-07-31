using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using ServerCore4;

namespace Server4
{
    class Program
    {
        static Listener _listener = new Listener();
        static void Main(string[] args)
        {
            //DNS (Domain Name System) 이름으로 ip주소를 찾아가는 시스템
            string host = Dns.GetHostName(); //컴퓨터의 호스트이름 get
            IPHostEntry ipHost = Dns.GetHostEntry(host); //ip주소를 담고 있는 인스턴스
            IPAddress ipAddress = ipHost.AddressList[0]; //ip주소 가져옴
            IPEndPoint endPoint = new IPEndPoint(ipAddress, 7777); //최종 목적지
            //www.google.com -> 123. 123. 123. 12


            _listener.Init(endPoint, () => { return new ClientSession(); });
            Console.WriteLine("Listening...");

            while (true)
            {
                ;
            }
        }
    }
}
