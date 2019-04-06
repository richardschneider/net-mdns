using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Makaretu.Dns
{
    static class LinuxHelper
    {
#if NETSTANDARD2_0
        // see https://github.com/richardschneider/net-mdns/issues/22
        public static unsafe void ReuseAddresss(Socket socket)
        {
            int setval = 1;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                int rv = Tmds.Linux.LibC.setsockopt(
                    socket.Handle.ToInt32(),
                    Tmds.Linux.LibC.SOL_SOCKET,
                    Tmds.Linux.LibC.SO_REUSEADDR,
                    &setval, sizeof(int));
                if (rv != 0)
                {
                    throw new Exception("Socet reuse addr failed.");
                    //todo: throw new PlatformException();
                }
            }
        }
#endif

    }
}
