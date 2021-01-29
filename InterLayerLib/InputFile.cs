using System;
using System.IO;

namespace InterLayerLib
{
    public class InputFile
    {
        public string localPath;
        public string localName;
        public string remotePath;
        public string remoteName;
        public string remoteServer;
        public bool uploaded;

        public InputFile() { uploaded = false; }

        public InputFile(InputFile other)
        {
            this.localPath   = other.localPath;
            this.localName   = other.localName;
            this.remotePath  = other.remotePath;
            this.remoteName  = other.remoteName;
            this.remoteServer= other.remoteServer;
            this.uploaded    = other.uploaded;
        }

        public InputFile(string ln)
        {
            //ToolKit.Trace("New input file: " + ln);
            localPath = Path.GetDirectoryName(Path.GetFullPath(@ln));
            localName = Path.GetFileName(@ln);
            uploaded = false;
        }

        public void fillFromString(string name, string content)
        {
            localPath = Path.GetDirectoryName(Path.GetFullPath(@name));
            localName = Path.GetFileName(@name);
            File.WriteAllText(localFull(), content);
        }
        
        public bool sendToServer(ServerAddress sa, string workspaceID)
        {
            // TODO: make path relative to model root (decide whether to include the root too)
            remoteName = WebUtility.uploadFileFB(sa, workspaceID, localPath + "\\" + localName);
            uploaded = (remoteName.Trim().Length > 0);
            return uploaded;
        }
        
        public void uploadByAction(Action<string> uploader, string server, string rp)
        {
           // ToolKit.Trace("uploading via: " + "put \"" + localPath + "\\" + localName + "\"");
            uploader("put \"" + localPath + "\\" + localName + "\"");
            remoteServer = server;
            remoteName = localName;
            remotePath = rp;
            uploaded = true;
        }

        public bool valid()
        {
            return uploaded;
        }

        public string remoteAddress()
        {
            return (uploaded ? remoteServer + "/" + remoteName : "");
        }

        private string localFull()
        {
            return localPath + "\\" + localName;
        }
        private string remoteFull()
        {
            return remotePath + "/" + remoteName;
        }
    }
}
