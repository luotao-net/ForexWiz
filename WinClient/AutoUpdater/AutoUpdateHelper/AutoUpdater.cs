/*****************************************************************
 * Copyright (C) Leo Studio Corporation. All rights reserved.
 * 
 * Author:   Leo 
 * Email:    luotao.net@gmail.com
 * Website:  http://www.luotao.net
 *
*****************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;

namespace LeoStudio.Autoupdater
{
    #region The delegate
    public delegate void ShowHandler();
    #endregion

    public class AutoUpdater : IAutoUpdater
    {
        #region The private fields
        private Config config = null;
        private bool bNeedRestart = false;
        private bool bDownload = false;
        private string localPath = Application.StartupPath;
        List<DownloadFileInfo> downloadFileListTemp = null;
        #endregion

        #region The public event
        public event ShowHandler OnShow;
        #endregion

        #region The constructor of AutoUpdater
        public AutoUpdater(string appname)
        {
            ConstFile.APPLICATION_NAME = appname;
            config = Config.LoadConfig(Path.Combine(localPath, ConstFile.FILENAME));
            ConstFile.WEBSITE = config.WebSite;
        }
        #endregion

        #region The public method
        public void Update()
        {
            if (!config.Enabled)
                return;

            Dictionary<string, RemoteFile> listRemotFile = ParseRemoteXml(config.ConfigUrl);
            List<DownloadFileInfo> downloadList = new List<DownloadFileInfo>();

            //RemoteFile->DownloadFileInfo
            string thisAssemblyName = Path.GetFileName((Assembly.GetExecutingAssembly().CodeBase).ToLower());
            string thisExeName = Path.GetFileName(Application.ExecutablePath).ToLower();

            foreach (RemoteFile file in listRemotFile.Values)
            {
                string urlfilepath = file.Url;
                string localfilepath = Path.Combine(localPath, file.Path);
                FileInfo finfo = new FileInfo(localfilepath);
                //过滤掉exe和本更新文件
                if (finfo.Name.ToLower() == thisExeName
                   || finfo.Name.ToLower() == thisAssemblyName)
                {
                    continue;
                }
                //文件大小不一致需要更新
                if (!finfo.Exists || this.Md5(File.ReadAllBytes(finfo.FullName)) != file.Md5)
                {
                    downloadList.Add(new DownloadFileInfo(urlfilepath, localfilepath, file.Path, file.Timestamp, file.Size));
                }
            }

            downloadFileListTemp = downloadList;

            if (downloadFileListTemp != null && downloadFileListTemp.Count > 0)
            {
                DownloadConfirm dc = new DownloadConfirm(downloadList);

                if (this.OnShow != null)
                    this.OnShow();

                if (DialogResult.OK == dc.ShowDialog())
                {
                    StartDownload(downloadList);
                }
            }
        }

        public void RollBack()
        {
            foreach (DownloadFileInfo file in downloadFileListTemp)
            {
                string tempUrlPath = CommonUnitity.GetFolderUrl(file);
                string oldPath = string.Empty;
                try
                {
                    if (!string.IsNullOrEmpty(tempUrlPath))
                    {
                        oldPath = Path.Combine(CommonUnitity.SystemBinUrl + tempUrlPath.Substring(1), file.FileName);
                    }
                    else
                    {
                        oldPath = Path.Combine(CommonUnitity.SystemBinUrl, file.FileName);
                    }

                    if (oldPath.EndsWith("_"))
                        oldPath = oldPath.Substring(0, oldPath.Length - 1);

                    MoveFolderToOld(oldPath + ".old", oldPath);
                }
                catch (Exception ex)
                {
                    //log the error message,you can use the application's log code
                }
            }
        }


        #endregion

        #region The private method
        string newfilepath = string.Empty;
        private void MoveFolderToOld(string oldPath, string newPath)
        {
            if (File.Exists(oldPath) && File.Exists(newPath))
            {
                System.IO.File.Copy(oldPath, newPath, true);
            }
        }

        private void StartDownload(List<DownloadFileInfo> downloadList)
        {
            DownloadProgress dp = new DownloadProgress(downloadList);
            if (dp.ShowDialog() == DialogResult.OK)
            {
                if (DialogResult.Cancel == dp.ShowDialog())
                {
                    return;
                }

                if (bNeedRestart)
                {
                    //Delete the temp folder
                    Directory.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConstFile.TEMPFOLDERNAME), true);
                    MessageBox.Show(ConstFile.APPLYTHEUPDATE, ConstFile.MESSAGETITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    CommonUnitity.RestartApplication();
                }
            }
        }

        private Dictionary<string, RemoteFile> ParseRemoteXml(string xml)
        {
            XmlDocument document = new XmlDocument();
            document.Load(xml);

            Dictionary<string, RemoteFile> list = new Dictionary<string, RemoteFile>();
            foreach (XmlNode node in document.DocumentElement.SelectNodes("//file"))
            {
                string s = node.Attributes["name"].Value;
                list.Add(s, new RemoteFile(node));
            }

            return list;
        }
        #endregion

        public string GetUnixTimeStamp(DateTime dt)
        {
            DateTime dtStart = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            DateTime dtNow = DateTime.Parse(DateTime.Now.ToString());
            TimeSpan toNow = dt.Subtract(dtStart);
            string ts = toNow.Ticks.ToString();
            ts = ts.Substring(0, ts.Length - 7);
            return ts;
        }

        private string Md5(byte[] input)
        {
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] result = md5.ComputeHash(input);

            string strresult = "";
            for (int i = 0; i < result.Length; i++)
            {
                strresult += result[i].ToString("x").PadLeft(2, '0');
            }

            return strresult;
        }
    }

}
