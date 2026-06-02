using Velopack;

namespace Dh4Launcher;

public class Starter
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Velopack 설치/업데이트 훅 처리. 반드시 앱 시작 전 가장 먼저 호출해야 한다.
        VelopackApp.Build().Run();
        _ = new App().Run();
    }
}
