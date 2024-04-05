/* SPDX-License-Identifier: MIT
 *
 * Copyright (C) 2019-2022 WireGuard LLC. All Rights Reserved.
 */

using H.OpenVpn.Wireguard.TunnelDll;

namespace TunnH.OpenVpn.Wireguard.Tunnelel
{
    public class Keypair
    {
        public readonly string Public;
        public readonly string Private;

        public Keypair(string pub, string priv)
        {
            Public = pub;
            Private = priv;
        }

        public static Keypair Generate()
        {
            var publicKey = new byte[32];
            var privateKey = new byte[32];
            NativeMethods.WireGuardGenerateKeypair(publicKey, privateKey);
            return new Keypair(Convert.ToBase64String(publicKey), Convert.ToBase64String(privateKey));
        }
    }
}
