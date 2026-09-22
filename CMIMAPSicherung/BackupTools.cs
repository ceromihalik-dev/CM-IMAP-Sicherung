using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CMIMAPSicherung
{
    internal class VerificationResult
    {
        public bool Success;
        public int FileCount;
        public long TotalBytes;
        public string Message;
    }

    internal static class PowerManager
    {
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        public static void SetKeepAwake(bool enabled)
        {
            try
            {
                SetThreadExecutionState(enabled ? (ES_CONTINUOUS | ES_SYSTEM_REQUIRED) : ES_CONTINUOUS);
            }
            catch { }
        }
    }

    internal static class BackupTools
    {
        public const string ManifestName = "_cm_manifest.sha256";
        public const string ArchiveSecretName = "_cm_zip_archive.cred";

        public static VerificationResult CreateManifestAndVerify(string accountDir)
        {
            WriteManifest(accountDir);
            return VerifyManifest(accountDir);
        }

        public static VerificationResult VerifyManifest(string accountDir)
        {
            var result = new VerificationResult { Success = false, Message = "Unbekannter Prüfstatus" };
            if (String.IsNullOrWhiteSpace(accountDir) || !Directory.Exists(accountDir))
            {
                result.Message = "Backup-Ordner wurde nicht gefunden.";
                return result;
            }

            string manifest = Path.Combine(accountDir, ManifestName);
            if (!File.Exists(manifest))
            {
                result.Message = "Es ist noch kein Prüfnachweis vorhanden. Nach einer neuen Sicherung wird automatisch ein SHA-256-Prüfnachweis erstellt.";
                return result;
            }

            int checkedFiles = 0;
            long totalBytes = 0;
            foreach (string raw in File.ReadAllLines(manifest, Encoding.UTF8))
            {
                if (String.IsNullOrWhiteSpace(raw)) continue;
                int sep = raw.IndexOf('|');
                if (sep <= 0) continue;
                string expected = raw.Substring(0, sep).Trim();
                string rel = raw.Substring(sep + 1);
                string file = Path.Combine(accountDir, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file))
                {
                    result.Message = "Datei fehlt: " + rel;
                    return result;
                }
                string actual = ComputeSha256(file);
                if (!String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    result.Message = "Prüfsumme stimmt nicht: " + rel;
                    return result;
                }
                try { totalBytes += new FileInfo(file).Length; } catch { }
                checkedFiles++;
            }

            if (checkedFiles == 0)
            {
                result.Message = "Der Prüfnachweis enthält keine Sicherungsdateien.";
                return result;
            }

            result.Success = true;
            result.FileCount = checkedFiles;
            result.TotalBytes = totalBytes;
            result.Message = String.Format("Backup-Prüfung erfolgreich: {0} Datei(en), {1}.", checkedFiles, FormatBytes(totalBytes));
            return result;
        }

        public static void WriteManifest(string accountDir)
        {
            if (!Directory.Exists(accountDir)) throw new DirectoryNotFoundException(accountDir);
            string manifest = Path.Combine(accountDir, ManifestName);
            var files = Directory.EnumerateFiles(accountDir, "*", SearchOption.AllDirectories)
                .Where(f => !String.Equals(Path.GetFileName(f), ManifestName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0) throw new InvalidOperationException("Im Backup-Ordner wurden keine Sicherungsdateien gefunden.");

            using (var sw = new StreamWriter(manifest, false, new UTF8Encoding(false)))
            {
                foreach (string file in files)
                {
                    string rel = MakeRelativePath(accountDir, file).Replace(Path.DirectorySeparatorChar, '/');
                    sw.WriteLine(ComputeSha256(file) + "|" + rel);
                }
            }
        }

        private static string MakeRelativePath(string root, string file)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fileFull = Path.GetFullPath(file);
            if (fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) return fileFull.Substring(rootFull.Length);
            return Path.GetFileName(fileFull);
        }

        private static string ComputeSha256(string file)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = sha.ComputeHash(fs);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        public static bool TryGetFreeSpace(string path, out long freeBytes, out long totalBytes)
        {
            freeBytes = 0;
            totalBytes = 0;
            try
            {
                string full = Path.GetFullPath(path);
                string root = Path.GetPathRoot(full);
                if (String.IsNullOrWhiteSpace(root)) return false;
                var drive = new DriveInfo(root);
                if (!drive.IsReady) return false;
                freeBytes = drive.AvailableFreeSpace;
                totalBytes = drive.TotalSize;
                return true;
            }
            catch { return false; }
        }

        public static bool TryGetDriveFormat(string path, out string format)
        {
            format = "";
            try
            {
                string full = Path.GetFullPath(path);
                string root = Path.GetPathRoot(full);
                if (String.IsNullOrWhiteSpace(root)) return false;
                var drive = new DriveInfo(root);
                if (!drive.IsReady) return false;
                format = drive.DriveFormat ?? "";
                return !String.IsNullOrWhiteSpace(format);
            }
            catch { return false; }
        }

        public static long GetDirectorySize(string dir)
        {
            if (String.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return 0;
            long total = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        public static List<FileInfo> GetLargeFiles(string dir, long thresholdBytes, int maxCount)
        {
            var result = new List<FileInfo>();
            if (String.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir) || thresholdBytes <= 0) return result;
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        if (fi.Length >= thresholdBytes) result.Add(fi);
                    }
                    catch { }
                }
            }
            catch { }
            return result.OrderByDescending(f => f.Length).Take(Math.Max(1, maxCount)).ToList();
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
            double value = Math.Max(0, bytes);
            int idx = 0;
            while (value >= 1024 && idx < units.Length - 1) { value /= 1024.0; idx++; }
            return value.ToString(idx == 0 ? "0" : "0.0") + " " + units[idx];
        }

        public static PythonCommand DetectPython()
        {
            var candidates = new List<PythonCommand>();
            candidates.Add(new PythonCommand { FileName = "py.exe", Prefix = new List<string> { "-3" } });
            candidates.Add(new PythonCommand { FileName = "python.exe", Prefix = new List<string>() });
            foreach (var c in candidates)
            {
                int rc; string output;
                try { RunPython(c, new List<string> { "--version" }, null, null, out rc, out output); if (rc == 0) return c; } catch { }
            }
            return null;
        }

        public static bool CheckModule(PythonCommand py, string module)
        {
            int rc; string output;
            RunPython(py, new List<string> { "-c", "import " + module + "; print('OK')" }, null, null, out rc, out output);
            return rc == 0;
        }

        public static bool InstallModule(PythonCommand py, string package, out string output)
        {
            int rc;
            RunPython(py, new List<string> { "-m", "pip", "install", "--user", package }, null, null, out rc, out output);
            return rc == 0;
        }

        public static bool SaveProtectedSecret(string secretFile, string password, out string error)
        {
            error = "";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(secretFile));
                var psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$ErrorActionPreference='Stop'; $p=[Console]::In.ReadToEnd(); $s=ConvertTo-SecureString $p -AsPlainText -Force; ConvertFrom-SecureString -SecureString $s | Set-Content -LiteralPath $env:SECRET_FILE -Encoding ASCII\"";
                psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardInput = true; psi.RedirectStandardError = true;
                psi.EnvironmentVariables["SECRET_FILE"] = secretFile;
                using (var p = Process.Start(psi))
                {
                    p.StandardInput.Write(password); p.StandardInput.Close();
                    error = p.StandardError.ReadToEnd(); p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public static bool DecryptProtectedSecret(string secretFile, string tempFile, out string error)
        {
            error = "";
            try
            {
                if (!File.Exists(secretFile)) { error = "Geschütztes Kennwort fehlt."; return false; }
                Directory.CreateDirectory(Path.GetDirectoryName(tempFile));
                var psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$ErrorActionPreference='Stop'; $e=(Get-Content -Raw -LiteralPath $env:SECRET_FILE).Trim(); $s=ConvertTo-SecureString $e; $b=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($s); try { $p=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($b); $enc=New-Object System.Text.UTF8Encoding($false); [IO.File]::WriteAllText($env:TEMP_PASS,$p,$enc) } finally { if($b -ne [IntPtr]::Zero){ [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) } }\"";
                psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardError = true;
                psi.EnvironmentVariables["SECRET_FILE"] = secretFile; psi.EnvironmentVariables["TEMP_PASS"] = tempFile;
                using (var p = Process.Start(psi))
                {
                    error = p.StandardError.ReadToEnd(); p.WaitForExit(); return p.ExitCode == 0;
                }
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public static int CreateEncryptedZip(PythonCommand py, string sourceDir, string zipPath, string passwordFile, string tempDir, Func<bool> cancel, out string output)
        {
            string script = Path.Combine(tempDir, "cm_zip_create.py");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(script, ZipScript, new UTF8Encoding(false));
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
            int rc;
            RunPython(py, new List<string> { script, sourceDir, zipPath, passwordFile }, null, cancel, out rc, out output);
            return rc;
        }

        public static int RestoreToImap(PythonCommand py, AccountConfig account, string imapPasswordFile, string sourcePath, string archivePasswordFile, string tempDir, Func<bool> cancel, out string output)
        {
            return RestoreToImap(py, account, imapPasswordFile, sourcePath, archivePasswordFile, tempDir, 600, cancel, out output);
        }

        public static int RestoreToImap(PythonCommand py, AccountConfig account, string imapPasswordFile, string sourcePath, string archivePasswordFile, string tempDir, int timeoutSeconds, Func<bool> cancel, out string output)
        {
            string script = Path.Combine(tempDir, "cm_imap_restore.py");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(script, RestoreScript, new UTF8Encoding(false));
            int rc;
            RunPython(py, new List<string> { script, account.Host, account.Port.ToString(), account.User, imapPasswordFile, sourcePath, archivePasswordFile ?? "", Math.Max(60, timeoutSeconds).ToString() }, null, cancel, out rc, out output);
            return rc;
        }

        public static void CleanupArchives(string archiveDir, int keepCount)
        {
            CleanupArchives(archiveDir, keepCount, 0);
        }

        public static void CleanupArchives(string archiveDir, int keepCount, long maxBytes)
        {
            if (!Directory.Exists(archiveDir)) return;
            try
            {
                var files = new DirectoryInfo(archiveDir).GetFiles("*.zip").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
                if (keepCount > 0)
                {
                    foreach (var f in files.Skip(keepCount).ToList())
                    {
                        try { f.Delete(); } catch { }
                    }
                }

                if (maxBytes > 0)
                {
                    files = new DirectoryInfo(archiveDir).GetFiles("*.zip").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
                    long total = files.Sum(f => f.Length);
                    for (int i = files.Count - 1; i >= 0 && total > maxBytes; i--)
                    {
                        try
                        {
                            long len = files[i].Length;
                            files[i].Delete();
                            total -= len;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static void RunPython(PythonCommand py, List<string> args, string workingDir, Func<bool> cancel, out int exitCode, out string output)
        {
            var all = new List<string>(); all.AddRange(py.Prefix); all.AddRange(args);
            var psi = new ProcessStartInfo();
            psi.FileName = py.FileName; psi.Arguments = JoinArgs(all); psi.UseShellExecute = false; psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            if (!String.IsNullOrWhiteSpace(workingDir)) psi.WorkingDirectory = workingDir;
            using (var p = Process.Start(psi))
            {
                var sbOut = new StringBuilder(); var sbErr = new StringBuilder();
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) { lock (sbOut) sbOut.AppendLine(e.Data); } };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) { lock (sbErr) sbErr.AppendLine(e.Data); } };
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                while (!p.WaitForExit(250))
                {
                    if (cancel != null && cancel()) { try { p.Kill(); } catch { } exitCode = 1223; output = "Abgebrochen"; return; }
                }
                p.WaitForExit(); exitCode = p.ExitCode;
                output = (sbOut.ToString() + Environment.NewLine + sbErr.ToString()).Trim();
            }
        }

        private static string JoinArgs(IEnumerable<string> args) { return String.Join(" ", args.Select(QuoteArg).ToArray()); }

        private static string QuoteArg(string arg)
        {
            if (arg == null) return "\"\"";
            if (arg.Length > 0 && arg.IndexOfAny(new char[] { ' ', '\t', '\n', '\v', '"' }) < 0) return arg;
            var sb = new StringBuilder(); sb.Append('"'); int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\') backslashes++;
                else if (c == '"') { sb.Append('\\', backslashes * 2 + 1); sb.Append('"'); backslashes = 0; }
                else { sb.Append('\\', backslashes); backslashes = 0; sb.Append(c); }
            }
            sb.Append('\\', backslashes * 2); sb.Append('"'); return sb.ToString();
        }

        private const string ZipScript = @"import os, sys
import pyzipper
source, target, passfile = sys.argv[1:4]
with open(passfile, 'rb') as f: pwd=f.read()
if not pwd: raise SystemExit('ZIP password is empty')
tmp=target+'.tmp'
if os.path.exists(tmp): os.remove(tmp)
with pyzipper.AESZipFile(tmp, 'w', compression=pyzipper.ZIP_DEFLATED, encryption=pyzipper.WZ_AES, allowZip64=True) as z:
    z.setpassword(pwd)
    z.setencryption(pyzipper.WZ_AES, nbits=256)
    count=0
    total=0
    for root, dirs, files in os.walk(source):
        for name in files:
            path=os.path.join(root,name)
            rel=os.path.relpath(path, source)
            z.write(path, rel)
            count += 1
            try: total += os.path.getsize(path)
            except OSError: pass
            if count % 25 == 0:
                print('ZIP_PROGRESS files=%d bytes=%d' % (count,total), flush=True)
if os.path.exists(target): os.remove(target)
os.replace(tmp,target)
with pyzipper.AESZipFile(target,'r',allowZip64=True) as z:
    z.setpassword(pwd)
    bad=z.testzip()
    if bad: raise SystemExit('ZIP verification failed: '+bad)
print('ZIP_OK files=%d source_bytes=%d archive_bytes=%d path=%s' % (count,total,os.path.getsize(target),target))
";

        private const string RestoreScript = @"import os, sys, imaplib, mailbox, tempfile, shutil, re
host=sys.argv[1]; port=int(sys.argv[2]); user=sys.argv[3]; passfile=sys.argv[4]; source=sys.argv[5]; zippass=sys.argv[6] if len(sys.argv)>6 else ''; timeout=int(sys.argv[7]) if len(sys.argv)>7 else 600
with open(passfile,'r',encoding='utf-8') as f: pwd=f.read()

def folder_from_rel(rel, delim):
    stem=rel[:-5] if rel.lower().endswith('.mbox') else rel
    stem=stem.replace('\\','/').replace('/','.')
    return stem if delim=='.' else stem.replace('.',delim)

def restore_mbox(m, path, rel, delim):
    folder=folder_from_rel(rel,delim)
    if folder.upper()!='INBOX':
        try: m.create(folder)
        except Exception: pass
    box=mailbox.mbox(path, create=False)
    count=0
    try:
        for msg in box:
            raw=msg.as_bytes()
            typ,data=m.append(folder,None,None,raw)
            if typ!='OK': raise RuntimeError('APPEND failed for '+folder)
            count += 1
            if count % 250 == 0:
                print('RESTORE_PROGRESS folder=%s messages=%d' % (folder,count), flush=True)
    finally:
        try: box.close()
        except Exception: pass
    print('RESTORED folder=%s messages=%d' % (folder,count), flush=True)
    return count

m=None
tmp=None
try:
    if port==993:
        m=imaplib.IMAP4_SSL(host,port,timeout=timeout)
    else:
        m=imaplib.IMAP4(host,port,timeout=timeout)
        m.starttls()
    m.login(user,pwd)
    delim='.'
    try:
        typ,data=m.list('', '')
        if typ=='OK' and data and data[0]:
            row=data[0].decode(errors='replace')
            mt=re.search(r'\)\s+\x22([^\x22]*)\x22',row)
            if mt and mt.group(1): delim=mt.group(1)
    except Exception: pass

    total=0
    if source.lower().endswith('.zip'):
        import pyzipper
        if not zippass: raise SystemExit('ZIP password file missing')
        with open(zippass,'rb') as f: zp=f.read()
        tmp=tempfile.mkdtemp(prefix='cmimap_restore_')
        with pyzipper.AESZipFile(source,'r',allowZip64=True) as z:
            z.setpassword(zp)
            names=sorted([n for n in z.namelist() if n.lower().endswith('.mbox')])
            if not names: raise SystemExit('No .mbox files found')
            for idx,name in enumerate(names,1):
                local=os.path.join(tmp,'current.mbox')
                if os.path.exists(local): os.remove(local)
                with z.open(name,'r') as src, open(local,'wb') as dst:
                    shutil.copyfileobj(src,dst,1024*1024)
                print('RESTORE_ZIP_FILE %d/%d %s bytes=%d' % (idx,len(names),name,os.path.getsize(local)), flush=True)
                total += restore_mbox(m,local,name,delim)
                try: os.remove(local)
                except Exception: pass
    else:
        files=[]
        for root,dirs,names in os.walk(source):
            for name in names:
                if name.lower().endswith('.mbox'):
                    path=os.path.join(root,name)
                    files.append((path,os.path.relpath(path,source)))
        files.sort(key=lambda x:x[1].lower())
        if not files: raise SystemExit('No .mbox files found')
        for path,rel in files:
            total += restore_mbox(m,path,rel,delim)

    print('RESTORE_OK messages=%d' % total)
finally:
    if m is not None:
        try: m.logout()
        except Exception: pass
    if tmp: shutil.rmtree(tmp,ignore_errors=True)
";
    }
}
