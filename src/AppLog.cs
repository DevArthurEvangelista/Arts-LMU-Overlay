using System;
using System.Collections.Generic;
using System.IO;

namespace Arthur.LmuOverlay
{
    static class AppLog
    {
        static readonly object gate=new object();
        static readonly Dictionary<string,DateTime> recent=new Dictionary<string,DateTime>();
        public static void Write(string context,Exception error)
        {
            lock(gate)
            {
                DateTime last,now=DateTime.UtcNow;
                if(error!=null && recent.TryGetValue(context,out last) && (now-last).TotalSeconds<10) return;
                recent[context]=now;
                try
                {
                    Directory.CreateDirectory(AppSettings.DirectoryPath);
                    string path=Path.Combine(AppSettings.DirectoryPath,"erro.txt");
                    if(File.Exists(path) && new FileInfo(path).Length>256*1024) { File.Copy(path,path+".anterior",true);File.WriteAllText(path,""); }
                    File.AppendAllText(path,DateTime.Now.ToString("s")+" • "+AppInfo.Version+" • "+context+Environment.NewLine+(error==null ? "" : error+Environment.NewLine));
                }
                catch(IOException) { } catch(UnauthorizedAccessException) { }
            }
        }
    }
}
