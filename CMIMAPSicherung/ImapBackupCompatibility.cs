using System;
using System.IO;

namespace CMIMAPSicherung
{
    internal static class ImapBackupCompatibility
    {
        private const string ScriptBase64 = "aW1wb3J0IGhhc2hsaWIKZnJvbSBpbWFwYmFja3VwMyBpbXBvcnQgdHJhbnNwb3J0CmZyb20gaW1hcGJhY2t1cDMuZXhjZXB0aW9ucyBpbXBvcnQgU2tpcEZvbGRlckV4Y2VwdGlvbgoKCmRlZiBfZGVjb2RlX2hlYWRlcihyYXcpOgogICAgZm9yIGVuYyBpbiAoInV0Zi04IiwgImxhdGluMSIpOgogICAgICAgIHRyeToKICAgICAgICAgICAgcmV0dXJuIHJhdy5kZWNvZGUoZW5jKQogICAgICAgIGV4Y2VwdCBVbmljb2RlRGVjb2RlRXJyb3I6CiAgICAgICAgICAgIHBhc3MKICAgIHJldHVybiByYXcuZGVjb2RlKCJ1dGYtOCIsIGVycm9ycz0icmVwbGFjZSIpCgoKZGVmIF9yb2J1c3Rfc2Nhbl9mb2xkZXIoc2VsZiwgZm9sZGVybmFtZSk6CiAgICBhc3NlcnQgc2VsZi5zZXJ2ZXIgaXMgbm90IE5vbmUKICAgIG1lc3NhZ2VzID0ge30KICAgIHRyYW5zcG9ydC5sb2dnZXIuaW5mbygiRm9sZGVyICVzIC4uLiIsIGZvbGRlcm5hbWUpCiAgICB0eXAsIGxpc3RfZGF0YSA9IHNlbGYuc2VydmVyLnNlbGVjdCh0cmFuc3BvcnQuX3F1b3RlX21haWxib3goZm9sZGVybmFtZSksIHJlYWRvbmx5PVRydWUpCiAgICBpZiB0eXAgIT0gIk9LIjoKICAgICAgICByYWlzZSBTa2lwRm9sZGVyRXhjZXB0aW9uKCJTRUxFQ1QgZmFpbGVkOiAlcyIgJSAobGlzdF9kYXRhLCkpCiAgICBudW1fbXNnc19yYXcgPSBsaXN0X2RhdGFbMF0KICAgIGlmIG51bV9tc2dzX3JhdyBpcyBOb25lOgogICAgICAgIHJhaXNlIFNraXBGb2xkZXJFeGNlcHRpb24oIlNFTEVDVCByZXR1cm5lZCBubyBtZXNzYWdlIGNvdW50IikKICAgIG51bV9tc2dzID0gaW50KG51bV9tc2dzX3JhdykKCiAgICBmb3IgbnVtIGluIHJhbmdlKDEsIG51bV9tc2dzICsgMSk6CiAgICAgICAgdHlwLCBmZXRjaF9kYXRhID0gc2VsZi5zZXJ2ZXIuZmV0Y2goc3RyKG51bSksICIoQk9EWS5QRUVLW0hFQURFUi5GSUVMRFMgKE1FU1NBR0UtSUQpXSkiKQogICAgICAgIGlmIHR5cCAhPSAiT0siIG9yIG5vdCBmZXRjaF9kYXRhIG9yIG5vdCBmZXRjaF9kYXRhWzBdOgogICAgICAgICAgICByYWlzZSBTa2lwRm9sZGVyRXhjZXB0aW9uKCJGRVRDSCAlcyBmYWlsZWQ6ICVzIiAlIChudW0sIGZldGNoX2RhdGEpKQoKICAgICAgICBoZWFkZXIgPSB0cmFuc3BvcnQuX2ZldGNoX3BheWxvYWQoZmV0Y2hfZGF0YVswXSkuc3RyaXAoKQogICAgICAgIGhlYWRlcl9zdHIgPSB0cmFuc3BvcnQuQkxBTktTX1JFLnN1YigiICIsIF9kZWNvZGVfaGVhZGVyKGhlYWRlcikpCiAgICAgICAgbWF0Y2ggPSB0cmFuc3BvcnQuTVNHSURfUkUubWF0Y2goaGVhZGVyX3N0cikKICAgICAgICB0cnk6CiAgICAgICAgICAgIG1zZ19pZCA9IG1hdGNoLmdyb3VwKDEpCiAgICAgICAgICAgIGlmIG1zZ19pZCBub3QgaW4gbWVzc2FnZXM6CiAgICAgICAgICAgICAgICBtZXNzYWdlc1ttc2dfaWRdID0gbnVtCiAgICAgICAgZXhjZXB0IChJbmRleEVycm9yLCBBdHRyaWJ1dGVFcnJvcik6CiAgICAgICAgICAgIHR5cCwgZmV0Y2hfZGF0YSA9IHNlbGYuc2VydmVyLmZldGNoKHN0cihudW0pLCAiKEJPRFlbSEVBREVSLkZJRUxEUyAoRlJPTSBUTyBDQyBEQVRFIFNVQkpFQ1QpXSkiKQogICAgICAgICAgICBpZiB0eXAgIT0gIk9LIiBvciBub3QgZmV0Y2hfZGF0YSBvciBub3QgZmV0Y2hfZGF0YVswXToKICAgICAgICAgICAgICAgIHJhaXNlIFNraXBGb2xkZXJFeGNlcHRpb24oIkZFVENIICVzIGZhaWxlZDogJXMiICUgKG51bSwgZmV0Y2hfZGF0YSkpCiAgICAgICAgICAgIHJhdyA9IHRyYW5zcG9ydC5fZmV0Y2hfcGF5bG9hZChmZXRjaF9kYXRhWzBdKQogICAgICAgICAgICBoZWFkZXJfc3RyID0gX2RlY29kZV9oZWFkZXIocmF3KS5zdHJpcCgpLnJlcGxhY2UoIlxyXG4iLCAiXHQiKQogICAgICAgICAgICAjIEhhc2ggdGhlIG9yaWdpbmFsIGJ5dGVzLCBub3QgdGhlIGRlY29kZWQgcmVwcmVzZW50YXRpb24uIFRoaXMgcmVtYWlucyBzdGFibGUKICAgICAgICAgICAgIyBldmVuIGZvciBtYWxmb3JtZWQgbGVnYWN5IGhlYWRlcnMgdGhhdCBhcmUgbm90IHZhbGlkIFVURi04LgogICAgICAgICAgICBtc2dfaWQgPSAiPCVzLiVzPiIgJSAodHJhbnNwb3J0LlVVSUQsIGhhc2hsaWIuc2hhMShyYXcpLmhleGRpZ2VzdCgpKQogICAgICAgICAgICBtZXNzYWdlc1ttc2dfaWRdID0gbnVtCgogICAgdHJhbnNwb3J0LmxvZ2dlci5pbmZvKCJGb3VuZCAlZCBtZXNzYWdlcyIsIGxlbihtZXNzYWdlcykpCiAgICByZXR1cm4gbWVzc2FnZXMKCgp0cmFuc3BvcnQuTWFpbFNlcnZlckhhbmRsZXIuc2Nhbl9mb2xkZXIgPSBfcm9idXN0X3NjYW5fZm9sZGVyCgpmcm9tIGltYXBiYWNrdXAzLmNsaSBpbXBvcnQgbWFpbgptYWluKCkK";

        public static string EnsureScript(string tempDir)
        {
            Directory.CreateDirectory(tempDir);
            string path = Path.Combine(tempDir, "cm_imapbackup_compat.py");
            byte[] data = Convert.FromBase64String(ScriptBase64);
            bool write = true;
            try
            {
                if (File.Exists(path))
                {
                    byte[] current = File.ReadAllBytes(path);
                    write = current.Length != data.Length;
                    if (!write)
                    {
                        for (int i = 0; i < data.Length; i++)
                            if (current[i] != data[i]) { write = true; break; }
                    }
                }
            }
            catch { write = true; }
            if (write) File.WriteAllBytes(path, data);
            return path;
        }
    }
}
