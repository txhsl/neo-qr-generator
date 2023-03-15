using Neo;
using Neo.Network.P2P.Payloads;
using Neo.VM;
using Neo.Wallets;
using Neo.Network.RPC;
using QRCoder;
using System.Runtime.Versioning;
using System.Numerics;
using Neo.SmartContract;
using Neo.Network.RPC.Models;

namespace QRGenerator
{
    [SupportedOSPlatform("windows")]
    class Program
    {
        private const int KEY_AMOUNT = 500;
        private const String PREFIX = "https://allweb.ngd.network:446/?wif=";

        private static List<WalletAccount> GenerateAccounts(NeoSystem system)
        {
            var wallet = Wallet.Open(@"D:\NEO\neo-qr-generator\QRGenerator\QRGenerator\Wallets\target.json", "", system.Settings);
            var accounts = new List<WalletAccount>();

            for (int i = 0; i < KEY_AMOUNT; i++)
            {
                var account = wallet.CreateAccount();
                accounts.Add(account);
            }

            wallet.Save();
            return accounts;
        }

        private static void SendGAS(NeoSystem system, RpcClient client, List<WalletAccount> accounts)
        {
            var source = Wallet.Open(@"D:\NEO\neo-qr-generator\QRGenerator\QRGenerator\Wallets\source.json", "", system.Settings);
            var keyPair = source.GetAccounts().First().GetKey();
            var GAS = Neo.SmartContract.Native.NativeContract.GAS.Hash;

            // Send 0.2 GAS to each account
            var sb = new ScriptBuilder();
            foreach (var account in accounts)
            {
                sb.EmitDynamicCall(GAS, "transfer", GetScriptHash(keyPair), GetScriptHash(account.GetKey()), (BigInteger)20_000_000, "");
            }
            byte[] script = sb.ToArray();
            Signer[] signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = GetScriptHash(keyPair) } };
            //InvokeScript(client, script, signers);
            SignAndSendTx(client, script, signers, keyPair);
        }

        private static List<string> GenerateContents(List<WalletAccount> accounts)
        {
            var contents = new List<string>();
            foreach (var account in accounts)
            {
                var content = PREFIX + account.GetKey().Export();
                contents.Add(content);
            }
            return contents;
        }

        private static void GenerateQRCodes(List<string> cotents)
        {
            var encoder = new QRCodeGenerator();
            var count = 505;

            foreach (var content in cotents)
            {
                var data = encoder.CreateQrCode(content, QRCodeGenerator.ECCLevel.M, true);
                var qrcode = new QRCode(data);
                var image = qrcode.GetGraphic(20);
                image.Save(@"D:\NEO\neo-qr-generator\QRGenerator\QRGenerator\Images\image-" + ++count + ".png");
            }
        }

        public static void Main()
        {
            // Wallet settings
            var protocol = ProtocolSettings.Load("config.json");
            var system = new NeoSystem(protocol);

            // RPC settings
            var client = new RpcClient(new Uri("http://seed2.neo.org:10332"), null, null, protocol);

            var accounts = GenerateAccounts(system);
            SendGAS(system, client, accounts);
            var contents = GenerateContents(accounts);
            GenerateQRCodes(contents);
        }

        private static void SignAndSendTx(RpcClient client, byte[] script, Signer[] signers, params KeyPair[] keyPair)
        {
            TransactionManagerFactory factory = new TransactionManagerFactory(client);
            TransactionManager manager = factory.MakeTransactionAsync(script, signers).Result;

            foreach (var kp in keyPair)
            {
                manager.AddSignature(kp);
            }

            Transaction invokeTx = manager.SignAsync().Result;

            var result = client.SendRawTransactionAsync(invokeTx);

            Console.WriteLine($"Transaction {invokeTx.Hash} is broadcasted!");
            Console.WriteLine(result.Result);
        }

        private static void InvokeScript(RpcClient client, byte[] script, params Signer[] signers)
        {
            RpcInvokeResult invokeResult = client.InvokeScriptAsync(script, signers).Result;

            Console.WriteLine($"Invoke result: {invokeResult.ToJson()}");
        }

        public static UInt160 GetScriptHash(KeyPair keyPair) => Contract.CreateSignatureContract(keyPair.PublicKey).ScriptHash;
    }
}