using System;
using System.Reflection;
using System.Threading;
using System.Collections;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;


using CUO_API;

namespace Assistant
{
    public enum ClientVersions
    {
        CV_OLD = (1 << 24) | (0 << 16) | (0 << 8) | 0, // Original game
        CV_200 = (2 << 24) | (0 << 16) | (0 << 8) | 0, // T2A Introduction. Adds screen dimensions packet
        CV_204C = (2 << 24) | (0 << 16) | (4 << 8) | 2, // Adds *.def files
        CV_305D = (3 << 24) | (0 << 16) | (5 << 8) | 3, // Renaissance. Expanded character slots.
        CV_306E = (3 << 24) | (0 << 16) | (0 << 8) | 0, // Adds a packet with the client type, switches to mp3 from midi for sound files
        CV_308D = (3 << 24) | (0 << 16) | (8 << 8) | 3, // Adds maximum stats to the status bar
        CV_308J = (3 << 24) | (0 << 16) | (8 << 8) | 9, // Adds followers to the status bar
        CV_308Z = (3 << 24) | (0 << 16) | (8 << 8) | 25, // Age of Shadows. Adds paladin, necromancer, custom housing, resists, profession selection window, removes save password checkbox
        CV_400B = (4 << 24) | (0 << 16) | (0 << 8) | 1, // Deletes tooltips
        CV_405A = (4 << 24) | (0 << 16) | (5 << 8) | 0, // Adds ninja, samurai
        CV_4011D = (4 << 24) | (0 << 16) | (11 << 8) | 3, // Adds elven race
        CV_500A = (5 << 24) | (0 << 16) | (0 << 8) | 0, // Paperdoll buttons journal becomes quests, chat becomes guild. Use mega FileManager.Cliloc. Removes verdata.mul.
        CV_5020 = (5 << 24) | (0 << 16) | (2 << 8) | 0, // Adds buff bar
        CV_5090 = (5 << 24) | (0 << 16) | (9 << 8) | 0, //
        CV_6000 = (6 << 24) | (0 << 16) | (0 << 8) | 0, // Adds colored guild/all chat and ignore system. New targeting systems, object properties and handles.
        CV_6013 = (6 << 24) | (0 << 16) | (1 << 8) | 3, //
        CV_6017 = (6 << 24) | (0 << 16) | (1 << 8) | 8, //
        CV_6040 = (6 << 24) | (0 << 16) | (4 << 8) | 0, // Increased number of player slots
        CV_6060 = (6 << 24) | (0 << 16) | (6 << 8) | 0, //
        CV_60142 = (6 << 24) | (0 << 16) | (14 << 8) | 2, //
        CV_60144 = (6 << 24) | (0 << 16) | (14 << 8) | 4, // Adds gargoyle race.
        CV_7000 = (7 << 24) | (0 << 16) | (0 << 8) | 0, //
        CV_7090 = (7 << 24) | (0 << 16) | (9 << 8) | 0, //
        CV_70130 = (7 << 24) | (0 << 16) | (13 << 8) | 0, //
        CV_70160 = (7 << 24) | (0 << 16) | (16 << 8) | 0, //
        CV_70180 = (7 << 24) | (0 << 16) | (18 << 8) | 0, //
        CV_70240 = (7 << 24) | (0 << 16) | (24 << 8) | 0, // *.mul -> *.uop
        CV_70331 = (7 << 24) | (0 << 16) | (33 << 8) | 1 //
    }

    public class Engine
    {
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.IsTerminating)
            {
                m_Running = false;

                new MessageDialog("Unhandled Exception", !e.IsTerminating, e.ExceptionObject.ToString()).ShowDialog(
                    Engine.ActiveWindow);
            }

            LogCrash(e.ExceptionObject as Exception);
        }

        public static void LogCrash(object exception)
        {
            if (exception == null || (exception is ThreadAbortException))
                return;

            using (StreamWriter txt = new StreamWriter("Crash.log", true))
            {
                txt.AutoFlush = true;
                txt.WriteLine("Exception @ {0}", Engine.MistedDateTime.ToString("MM-dd-yy HH:mm:ss.ffff"));
                txt.WriteLine(exception.ToString());
                txt.WriteLine("");
                txt.WriteLine("");
            }
        }

        public static ClientVersions ClientVersion { get; private set; }

        public static bool UseNewMobileIncoming => ClientVersion >= ClientVersions.CV_70331;

        public static bool UsePostHSChanges => ClientVersion >= ClientVersions.CV_7090;

        public static bool UsePostSAChanges => ClientVersion >= ClientVersions.CV_7000;

        public static bool UsePostKRPackets => ClientVersion >= ClientVersions.CV_6017;

        public static MainForm MainWindow
        {
            get { return m_MainWnd; }
        }

        public static bool Running
        {
            get { return m_Running; }
        }

        public static Form ActiveWindow
        {
            get { return m_ActiveWnd; }
            set { m_ActiveWnd = value; }
        }

        public static string Version
        {
            get
            {
                if (m_Version == null)
                {
                    Version v = Assembly.GetCallingAssembly().GetName().Version;
                    m_Version = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}"; //, v.Revision
                }

                return m_Version;
            }
        }

        public static string ShardList { get; private set; }

        private static MainForm m_MainWnd;

        private static Form m_ActiveWnd;

        //private static Thread m_TimerThread;
        private static bool m_Running;
        private static string m_Version;

        private static int _previousHour = -1;
        private static int _Differential;

        public static int Differential //to use in all cases where you rectify normal clocks obtained with utctimer!
        {
            get
            {
                if (_previousHour != DateTime.UtcNow.Hour)
                {
                    _previousHour = DateTime.UtcNow.Hour;
                    _Differential = Engine.MistedDateTime.Subtract(DateTime.UtcNow).Hours;
                }

                return _Differential;
            }
        }

        public static DateTime MistedDateTime
        {
            get { return DateTime.UtcNow.AddHours(Differential); }
        }

        private static string _rootPath = null;
        public static string RootPath => _rootPath ?? (_rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        [DllExport(CallingConvention.Cdecl)]
        public static unsafe void Install(PluginHeader *plugin)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                string[] fields = e.Name.Split(',');
                string name = fields[0];
                string culture = fields[2];

                if (name.EndsWith(".resources") && !culture.EndsWith("neutral"))
                {
                    return null;
                }
                AssemblyName askedassembly = new AssemblyName(e.Name);

                bool isdll = File.Exists(Path.Combine(RootPath, askedassembly.Name + ".dll"));

                return Assembly.LoadFile(Path.Combine(RootPath, askedassembly.Name + (isdll ? ".dll" : ".exe")));

            };

            ClientVersion = (ClientVersions)plugin->ClientVersion;

            if (!ClientCommunication.InstallHooks(ref plugin))
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }

            UOAssist.CreateWindow();

            string clientPath = Marshal.GetDelegateForFunctionPointer<OnGetUOFilePath>(plugin->GetUOFilePath)();

            Thread t = new Thread(() =>
            {
                m_Running = true;
                Thread.CurrentThread.Name = "Razor Main Thread";

#if !DEBUG
			    AppDomain.CurrentDomain.UnhandledException +=
                    new UnhandledExceptionEventHandler( CurrentDomain_UnhandledException );
#endif

                Ultima.Files.SetMulPath(clientPath);
                Ultima.Multis.PostHSFormat = UsePostHSChanges;

                if (!Language.Load("ENU"))
                {
                    MessageBox.Show(
                        "Fatal Error: Unable to load required file Language/Razor_lang.enu\nRazor cannot continue.",
                        "No Language Pack", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    return;
                }

                string defLang = Config.GetAppSetting<string>("DefaultLanguage");
                if (defLang != null && !Language.Load(defLang))
                    MessageBox.Show(
                        String.Format(
                            "WARNING: Razor was unable to load the file Language/Razor_lang.{0}\nENU will be used instead.",
                            defLang), "Language Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);


                Language.LoadCliLoc();

                Initialize(typeof(Assistant.Engine).Assembly); //Assembly.GetExecutingAssembly()

                Config.LoadCharList();
                if (!Config.LoadLastProfile())
                    MessageBox.Show(
                        "The selected profile could not be loaded, using default instead.", "Profile Load Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                m_MainWnd = new MainForm();
                Application.Run(m_MainWnd);
                m_Running = false;

                Counter.Save();
                Macros.MacroManager.Save();
                Config.Save();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        public static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private static void Initialize(Assembly a)
        {
            Type[] types = a.GetTypes();

            for (int i = 0; i < types.Length; i++)
            {
                MethodInfo init = types[i].GetMethod("Initialize", BindingFlags.Static | BindingFlags.Public);

                if (init != null)
                    init.Invoke(null, null);
            }
        }

        private static IPAddress Resolve(string addr)
        {
            IPAddress ipAddr = IPAddress.None;

            if (string.IsNullOrEmpty(addr))
                return ipAddr;

            try
            {
                ipAddr = IPAddress.Parse(addr);
            }
            catch
            {
                try
                {
                    IPHostEntry iphe = Dns.GetHostEntry(addr);

                    if (iphe.AddressList.Length > 0)
                        ipAddr = iphe.AddressList[iphe.AddressList.Length - 1];
                }
                catch
                {
                }
            }

            return ipAddr;
        }

        public static bool IsElevated
        {
            get
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}