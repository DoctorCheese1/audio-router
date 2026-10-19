using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace AudioRouter {
 public class Settings {
  public string Repository = "DoctorCheese1/audio-router";
  public bool CheckUpdates = true;
  public Dictionary<string,string> Routes = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
 }
 public class Output {
  public string Id, Name;
  public override string ToString() { return Name; }
 }
 public class AppItem {
  public string Exe, Name, Device, Volume, Muted;
 }
 public class Snapshot {
  public List<Output> Devices = new List<Output>();
  public List<AppItem> Apps = new List<AppItem>();
 }
 public class HttpClient : WebClient {
  public HttpClient() { Headers[HttpRequestHeader.UserAgent]="AudioRouter/1.0.0"; Encoding=Encoding.UTF8; }
  protected override WebRequest GetWebRequest(Uri address) {
   WebRequest r=base.GetWebRequest(address); r.Timeout=25000;
   var h=r as HttpWebRequest; if(h!=null) h.ReadWriteTimeout=25000;
   return r;
  }
 }
 public static class Backend {
  public const string Version="1.0.0";
  public static readonly string Data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AudioRouter");
  public static readonly string Helper=Path.Combine(Data,"engine","SoundVolumeView.exe");
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=16000000 };
  static readonly object Gate=new object();
  public static Settings Load() {
   Directory.CreateDirectory(Data);
   string f=Path.Combine(Data,"settings.json");
   if(!File.Exists(f))return new Settings();
   try { var s=Json.Deserialize<Settings>(File.ReadAllText(f)); if(s==null)throw new Exception(); if(s.Routes==null)s.Routes=new Dictionary<string,string>();return s; }
   catch { File.Copy(f,f+".invalid-"+DateTime.Now.ToString("yyyyMMddHHmmss"),true); return new Settings(); }
  }
  public static void Save(Settings s) {
   string f=Path.Combine(Data,"settings.json"),temp=f+".tmp";
   File.WriteAllText(temp,Json.Serialize(s),new UTF8Encoding(false));
   if(File.Exists(f))File.Replace(temp,f,null);else File.Move(temp,f);
  }
  // Quote arguments using the Windows CommandLineToArgvW backslash rules.
  public static string Quote(string text) {
   var b=new StringBuilder("\""); int slash=0;
   foreach(char c in text) { if(c=='\\'){slash++;continue;} if(c=='"'){b.Append('\\',slash*2+1);b.Append(c);}else{b.Append('\\',slash);b.Append(c);} slash=0; }
   b.Append('\\',slash*2);b.Append('"');return b.ToString();
  }
  public static void Command(params string[] args) {
   lock(Gate) {
    if(!File.Exists(Helper))throw new FileNotFoundException("Audio engine is missing. Use Install audio engine.");
    var start=new ProcessStartInfo(Helper,string.Join(" ",args.Select(Quote))) { UseShellExecute=false, CreateNoWindow=true, WorkingDirectory=Path.GetDirectoryName(Helper) };
    using(var p=Process.Start(start)) {
     if(!p.WaitForExit(15000)){try{p.Kill();}catch{}throw new TimeoutException("The audio engine did not respond. Try refreshing.");}
     if(p.ExitCode!=0)throw new Exception("Audio engine returned code "+p.ExitCode+". Refresh and try again.");
    }
   }
  }
  public static void Install() {
   Directory.CreateDirectory(Data);
   string zip=Path.Combine(Data,"engine-download.zip"),stage=Path.Combine(Data,"engine-"+Guid.NewGuid().ToString("N"));
   try {
    using(var c=new HttpClient())c.DownloadFile("https://www.nirsoft.net/utils/"+(Environment.Is64BitOperatingSystem?"soundvolumeview-x64.zip":"soundvolumeview.zip"),zip);
    ZipFile.ExtractToDirectory(zip,stage);
    if(!File.Exists(Path.Combine(stage,"SoundVolumeView.exe")))throw new Exception("The downloaded engine did not contain SoundVolumeView.exe.");
    string dest=Path.GetDirectoryName(Helper);Directory.CreateDirectory(dest);
    foreach(string file in Directory.GetFiles(stage))File.Copy(file,Path.Combine(dest,Path.GetFileName(file)),true);
   } finally { if(File.Exists(zip))File.Delete(zip); if(Directory.Exists(stage))Directory.Delete(stage,true); }
  }
  static string Get(Dictionary<string,object> row,string key) {
   object value;return row.TryGetValue(key,out value)&&value!=null?Convert.ToString(value,CultureInfo.InvariantCulture):"";
  }
  public static Snapshot Parse(string text) {
   var rows=Json.Deserialize<List<Dictionary<string,object>>>(text);
   if(rows==null)throw new Exception("Audio engine returned no device list.");
   var result=new Snapshot();
   foreach(var r in rows) {
    string type=Get(r,"Type"), direction=Get(r,"Direction");
    if(!direction.Equals("Render",StringComparison.OrdinalIgnoreCase))continue;
    if(type.Equals("Device",StringComparison.OrdinalIgnoreCase)) {
     if(!Get(r,"Device State").Equals("Active",StringComparison.OrdinalIgnoreCase))continue;
     string id=Get(r,"Item ID");if(id.Length==0)id=Get(r,"Command-Line Friendly ID");
     if(id.Length>0)result.Devices.Add(new Output{Id=id,Name=Get(r,"Name")+" — "+Get(r,"Device Name")});
    } else if(type.Equals("Application",StringComparison.OrdinalIgnoreCase)) {
     string exe=Get(r,"Process Path");if(exe.Length==0)exe=Get(r,"Process Name");
     try{exe=Path.GetFileName(exe);}catch{continue;}
     if(string.IsNullOrWhiteSpace(exe)||!exe.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))continue;
     result.Apps.Add(new AppItem{Exe=exe,Name=Get(r,"Name"),Device=Get(r,"Device Name"),Volume=Get(r,"Volume Percent"),Muted=Get(r,"Muted")});
    }
   }
   result.Devices=result.Devices.GroupBy(d=>d.Id).Select(g=>g.First()).ToList();
   result.Apps=result.Apps.GroupBy(a=>a.Exe,StringComparer.OrdinalIgnoreCase).Select(g=>new AppItem{Exe=g.Key,Name=g.First().Name,Device=string.Join("; ",g.Select(a=>a.Device).Distinct()),Volume=g.First().Volume,Muted=g.First().Muted}).OrderBy(a=>a.Exe).ToList();
   return result;
  }
  public static Snapshot Read() {
   string file=Path.Combine(Data,"audio-"+Guid.NewGuid().ToString("N")+".json");
   try {
    Command("/SaveFileEncoding","3","/sjson",file,"/Columns","Name,Type,Direction,Device Name,Device State,Item ID,Command-Line Friendly ID,Process Path,Process Name,Volume Percent,Muted");
    if(!File.Exists(file))throw new Exception("No audio list was created by the engine.");
    return Parse(File.ReadAllText(file,Encoding.UTF8).TrimStart('\uFEFF'));
   }finally{if(File.Exists(file))File.Delete(file);}
  }
  public static void Route(string exe,string device) { Command("/SetAppDefault",device,"all",exe); }
  public static bool ValidRepo(string s) { return Regex.IsMatch(s??"",@"^[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9_.-]+$") && !s.EndsWith("/.") && !s.EndsWith("/.."); }
  public static System.Version ReleaseVersion(string tag) {
   System.Version v;return System.Version.TryParse((tag??"").TrimStart('v','V'),out v)?v:null;
  }
  public static Dictionary<string,object> Latest(string repo) {
   if(!ValidRepo(repo))throw new Exception("Enter the GitHub repository as owner/repository.");
   using(var c=new HttpClient())return Json.Deserialize<Dictionary<string,object>>(c.DownloadString("https://api.github.com/repos/"+repo+"/releases/latest"));
  }
 }
 public class MainForm : Form {
  Settings settings;
  ListView apps=new ListView(); ComboBox device=new ComboBox(); TrackBar volume=new TrackBar();
  Label status=new Label(),selected=new Label(),percent=new Label();
  TextBox repo=new TextBox(); CheckBox autoUpdate=new CheckBox();
  FlowLayoutPanel buttons=new FlowLayoutPanel();
  bool busy=false; string updateUrl; Button updateButton;
  string SelectedExe { get { return apps.SelectedItems.Count==0?null:apps.SelectedItems[0].Tag as string; } }
  public MainForm() {
   settings=Backend.Load(); Text="Audio Router  ·  "+Backend.Version;MinimumSize=new Size(1080,700);Size=new Size(1180,760);StartPosition=FormStartPosition.CenterScreen;
   Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(19,27,42);ForeColor=Color.WhiteSmoke;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=8 };
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,53));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,78));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));Controls.Add(layout);
   layout.Controls.Add(new Label {Text="Audio Router",Font=new Font("Segoe UI",23,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
   layout.Controls.Add(new Label {Text="Choose a program, select its speakers or headset, then Apply output. Start audio if a program is missing.",Dock=DockStyle.Fill},0,1);
   apps.Dock=DockStyle.Fill;apps.View=View.Details;apps.FullRowSelect=true;apps.MultiSelect=false;apps.HideSelection=false;apps.BackColor=Color.FromArgb(28,40,60);apps.ForeColor=ForeColor;
   apps.Columns.Add("Program",200);apps.Columns.Add("Audio session",220);apps.Columns.Add("Currently playing through",330);apps.Columns.Add("Volume",85);apps.Columns.Add("Muted",65);
   apps.SelectedIndexChanged+=(s,e)=>SelectApp();layout.Controls.Add(apps,0,2);
   selected.Dock=DockStyle.Fill;selected.Padding=new Padding(0,10,0,0);selected.Text="Select a program above.";layout.Controls.Add(selected,0,3);
   var controls=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=4 };controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,55));
   device.Dock=DockStyle.Fill;device.DropDownStyle=ComboBoxStyle.DropDownList;controls.Controls.Add(device,0,0);
   controls.Controls.Add(new Label{Text="Volume",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter},1,0);
   volume.Minimum=0;volume.Maximum=100;volume.Value=100;volume.TickFrequency=10;volume.Dock=DockStyle.Fill;volume.ValueChanged+=(s,e)=>percent.Text=volume.Value+"%";controls.Controls.Add(volume,2,0);percent.Text="100%";percent.Dock=DockStyle.Fill;controls.Controls.Add(percent,3,0);layout.Controls.Add(controls,0,4);
   buttons.Dock=DockStyle.Fill;buttons.WrapContents=false;
   AddButton("Apply output",async()=>await Apply());AddButton("Set volume",async()=>await SetVolume());AddButton("Mute / unmute",async()=>await Mute());AddButton("Use Windows default",async()=>await Reset());AddButton("Refresh",async()=>await RefreshApps());AddButton("Install audio engine",async()=>await Install());layout.Controls.Add(buttons,0,5);
   var updates=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};updates.Controls.Add(new Label{Text="GitHub repo:",AutoSize=true,Margin=new Padding(0,9,5,0)});
   repo.Width=245;repo.Text=settings.Repository;updates.Controls.Add(repo);autoUpdate.Text="Check on startup";autoUpdate.Checked=settings.CheckUpdates;autoUpdate.AutoSize=true;autoUpdate.Margin=new Padding(10,8,5,0);updates.Controls.Add(autoUpdate);
   var check=MakeButton("Check updates",async()=>await CheckUpdate(true));updates.Controls.Add(check);updateButton=MakeButton("Open release",()=>{if(updateUrl!=null)Process.Start(new ProcessStartInfo(updateUrl){UseShellExecute=true});return Task.FromResult(0);});updateButton.Enabled=false;updates.Controls.Add(updateButton);layout.Controls.Add(updates,0,6);
   status.Dock=DockStyle.Fill;status.Text="Ready. Audio stays on this PC.";layout.Controls.Add(status,0,7);
   Shown+=async(s,e)=>{if(File.Exists(Backend.Helper))await RefreshApps();else status.Text="Click Install audio engine once to enable audio controls.";if(settings.CheckUpdates&&Backend.ValidRepo(settings.Repository))await CheckUpdate(false);};
   FormClosing+=(s,e)=>{try{SaveUpdateSettings(false);}catch{}};
  }
  Button MakeButton(string label,Func<Task> action) {
   var b=new Button {Text=label,AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(43,67,86),ForeColor=Color.White,Margin=new Padding(0,0,8,0)};
   b.Click+=async(s,e)=>{if(busy)return;busy=true;UseWaitCursor=true;try{await action();}catch(Exception ex){status.Text="Error: "+ex.Message;MessageBox.Show(this,ex.Message,"Audio Router",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{busy=false;UseWaitCursor=false;}};return b;
  }
  void AddButton(string label,Func<Task> action){buttons.Controls.Add(MakeButton(label,action));}
  void SelectApp() {
   string exe=SelectedExe;selected.Text=exe==null?"Select a program above.":"Selected: "+exe;
   if(exe==null)return;
   string id;if(settings.Routes.TryGetValue(exe,out id)){for(int i=0;i<device.Items.Count;i++)if(((Output)device.Items[i]).Id==id){device.SelectedIndex=i;break;}}
   string text=apps.SelectedItems[0].SubItems[3].Text.Trim().TrimEnd('%');double v;
   if(double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out v))volume.Value=Math.Max(0,Math.Min(100,(int)Math.Round(v)));
  }
  async Task RefreshApps() {
   string exe=SelectedExe;string old=device.SelectedItem==null?null:((Output)device.SelectedItem).Id;
   Snapshot snapshot=await Task.Run(()=>Backend.Read());
   device.Items.Clear();device.Items.Add(new Output{Id="DefaultRenderDevice",Name="Windows default output"});foreach(var d in snapshot.Devices)device.Items.Add(d);device.SelectedIndex=0;
   for(int i=0;i<device.Items.Count;i++)if(((Output)device.Items[i]).Id==old)device.SelectedIndex=i;
   apps.BeginUpdate();apps.Items.Clear();foreach(var a in snapshot.Apps){var row=new ListViewItem(new[]{a.Exe,a.Name,a.Device,a.Volume,a.Muted}){Tag=a.Exe};apps.Items.Add(row);if(a.Exe==exe)row.Selected=true;}apps.EndUpdate();
   SelectApp();status.Text=snapshot.Apps.Count+" audio programs found. Changes may require restarting playback in some apps.";
  }
  string RequireApp() {string exe=SelectedExe;if(exe==null)throw new Exception("Select a program first. Start it playing audio, then Refresh if it is missing.");return exe;}
  async Task Apply() {
   string exe=RequireApp();var output=device.SelectedItem as Output;if(output==null)throw new Exception("Select an output device.");
   await Task.Run(()=>Backend.Route(exe,output.Id));settings.Routes[exe]=output.Id;Backend.Save(settings);await RefreshApps();status.Text="Output preference saved for "+exe+". Restart playback if it has not moved yet.";
  }
  async Task SetVolume() {string exe=RequireApp();string value=volume.Value.ToString(CultureInfo.InvariantCulture);await Task.Run(()=>Backend.Command("/SetVolume",exe,value));await RefreshApps();}
  async Task Mute() {string exe=RequireApp();await Task.Run(()=>Backend.Command("/Switch",exe));await RefreshApps();}
  async Task Reset() {string exe=RequireApp();await Task.Run(()=>Backend.Route(exe,"DefaultRenderDevice"));settings.Routes.Remove(exe);Backend.Save(settings);await RefreshApps();status.Text=exe+" now follows the Windows default output preference.";}
  async Task Install() {
   if(MessageBox.Show(this,"Download the free SoundVolumeView audio engine from nirsoft.net? This is the local audio-control component. Its original help and license files are kept with it.","Install audio engine",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
   status.Text="Downloading audio engine from nirsoft.net...";await Task.Run(()=>Backend.Install());await RefreshApps();
  }
  void SaveUpdateSettings(bool validate) {
   string r=repo.Text.Trim();if(validate&&!Backend.ValidRepo(r))throw new Exception("Enter the repository as owner/repository, for example DoctorCheese1/audio-router.");
   settings.Repository=r;settings.CheckUpdates=autoUpdate.Checked;Backend.Save(settings);
  }
  async Task CheckUpdate(bool manual) {
   SaveUpdateSettings(manual);if(!Backend.ValidRepo(settings.Repository))return;
   status.Text="Checking GitHub Releases...";
   try {
    var release=await Task.Run(()=>Backend.Latest(settings.Repository));object raw;
    var latest=Backend.ReleaseVersion(release.TryGetValue("tag_name",out raw)?Convert.ToString(raw):"");
    if(latest==null)throw new Exception("The latest release tag is not a numeric version such as v1.0.1.");
    string expected="https://github.com/"+settings.Repository+"/releases/tag/";
    string url=release.TryGetValue("html_url",out raw)?Convert.ToString(raw):"";
    if(!url.StartsWith(expected,StringComparison.OrdinalIgnoreCase))throw new Exception("GitHub returned an unexpected release URL.");
    updateUrl=url;updateButton.Enabled=true;
    if(latest>new Version(Backend.Version)){status.Text="Update available: "+latest+". Click Open release to download it.";if(manual)MessageBox.Show(this,status.Text,"Audio Router update");}
    else status.Text="You're up to date ("+Backend.Version+").";
   } catch(WebException ex) {
    var response=ex.Response as HttpWebResponse;
    if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)status.Text="No published release found. Check the repo name; private repos need a public release feed.";
    else status.Text="Update check failed. Check your internet connection or GitHub rate limit.";
    if(manual)MessageBox.Show(this,status.Text,"Update check");
   } catch(Exception ex) {status.Text="Update check failed: "+ex.Message;if(manual)MessageBox.Show(this,status.Text,"Update check");}
  }
 }
 static class Program {
  [STAThread] static void Main(string[] args) {
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   if(args.Length>0&&args[0]=="--self-test"){SelfTest.Run();return;}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try{Application.Run(new MainForm());}catch(Exception e){MessageBox.Show(e.ToString(),"Audio Router startup error");}
  }
 }
 static class SelfTest {
  static void Assert(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);}
  public static void Run() {
   Assert(Backend.Quote("hello world")=="\"hello world\"","argument spaces");
   Assert(Backend.Quote("C:\\test\\")=="\"C:\\test\\\\\"","trailing slash quoting");
   Assert(Backend.Quote("a\"b")=="\"a\\\"b\"","embedded quote");
   Assert(Backend.ValidRepo("DoctorCheese1/audio-router"),"repository accepted");
   Assert(!Backend.ValidRepo("https://github.com/test/repo"),"URL rejected");
   Assert(!Backend.ValidRepo("owner/repo/evil"),"path rejected");
   Assert(Backend.ReleaseVersion("v1.2.0")>new Version("1.0.0"),"new release");
   Assert(Backend.ReleaseVersion("v1.0.0-beta")==null,"prerelease rejected");
   string sample="[{\"Type\":\"Device\",\"Direction\":\"Render\",\"Device State\":\"Active\",\"Item ID\":\"speaker-id\",\"Name\":\"Speakers\"},{\"Type\":\"Device\",\"Direction\":\"Capture\",\"Device State\":\"Active\",\"Item ID\":\"mic\"},{\"Type\":\"Device\",\"Direction\":\"Render\",\"Device State\":\"Disabled\",\"Item ID\":\"disabled\"},{\"Type\":\"Application\",\"Direction\":\"Render\",\"Process Name\":\"chrome.exe\",\"Volume Percent\":\"50%\"},{\"Type\":\"Application\",\"Direction\":\"Render\",\"Process Name\":\"chrome.exe\"}]";
   Snapshot s=Backend.Parse(sample);Assert(s.Devices.Count==1,"only active outputs");Assert(s.Apps.Count==1&&s.Apps[0].Exe=="chrome.exe","group chrome sessions");
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-passed.txt"),"All 10 core checks passed.");
  }
 }
}
