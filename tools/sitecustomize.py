"""Verified TLS defaults for the bundled legacy backup client. No insecure fallback."""
import imaplib
import ssl
_BaseSSL = imaplib.IMAP4_SSL
class VerifiedIMAP4SSL(_BaseSSL):
    def __init__(self, host='', port=993, keyfile=None, certfile=None, ssl_context=None, timeout=None):
        context = ssl_context or ssl.create_default_context()
        if certfile:
            context.load_cert_chain(certfile, keyfile)
        super().__init__(host, port, ssl_context=context, timeout=timeout)
imaplib.IMAP4_SSL = VerifiedIMAP4SSL
_BaseStartTLS = imaplib.IMAP4.starttls
def _verified_starttls(self, ssl_context=None):
    return _BaseStartTLS(self, ssl_context=ssl_context or ssl.create_default_context())
imaplib.IMAP4.starttls = _verified_starttls
