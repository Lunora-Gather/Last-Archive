// ============================================================
// Last Archive - 程序入口
// ============================================================

using System;

namespace LastArchive
{
    /// <summary>
    /// 程序入口
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // 如果参数包含 --test，运行自动化测试
            if (args.Length > 0 && args[0] == "--test")
            {
                try
                {
                    var test = new AutoTest();
                    test.RunAll();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[测试错误] {ex.Message}\n{ex.StackTrace}");
                }
                return;
            }

            // 如果参数包含 --gui，运行WinForms图形界面
            if (args.Length > 0 && args[0] == "--gui")
            {
                try
                {
                    var gui = new WinFormsGame();
                    Application.EnableVisualStyles();
                    Application.Run(gui);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GUI错误] {ex.Message}\n{ex.StackTrace}");
                }
                return;
            }

            // 如果参数包含 --web，运行Web服务器
            if (args.Length > 0 && args[0] == "--web")
            {
                try
                {
                    int port = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 8080;
                    var web = new WebGameServer();
                    web.Start(port);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Web错误] {ex.Message}\n{ex.StackTrace}");
                }
                return;
            }

            // 否则运行交互式控制台游戏
            try
            {
                var game = new ConsoleGame();
                game.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[致命错误] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("按任意键退出...");
                try { Console.ReadKey(); } catch { }
            }
        }
    }
}
