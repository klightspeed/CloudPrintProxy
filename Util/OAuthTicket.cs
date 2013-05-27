using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace TSVCEO.CloudPrint.Util
{
    public class OAuthTicket
    {
        private const string OAuthTokenRequestUri = "https://accounts.google.com/o/oauth2/token";

        protected DateTime AccessTokenExpires { get; set; }
        protected object AccessTokenLock { get; set; }
        protected string ClientId { get; set; }
        protected string ClientSecret { get; set; }
        protected string RedirectUri { get; set; }
        protected string _RefreshToken { get; set; }
        protected string _AccessToken { get; set; }
        protected string _TokenType { get; set; }

        public string AccessToken
        {
            get
            {
                RefreshAuthToken();
                return _AccessToken;
            }
        }

        public string TokenType
        {
            get
            {
                RefreshAuthToken();
                return _TokenType;
            }
        }

        public string RefreshToken
        {
            get
            {
                return _RefreshToken;
            }
        }

        protected void RefreshAuthToken()
        {
            lock (AccessTokenLock)
            {
                if (AccessTokenExpires < DateTime.Now)
                {
                    var req = HTTPHelper.CreateRequest(null, OAuthTokenRequestUri);
                    var reqdata = new
                    {
                        refresh_token = RefreshToken,
                        client_id = ClientId,
                        client_secret = ClientSecret,
                        grant_type = "refresh_token"
                    };

                    var respdata = HTTPHelper.ToJson(HTTPHelper.SendUrlEncodedPostData(req, reqdata));

                    _AccessToken = respdata.access_token;
                    _TokenType = respdata.token_type;
                    AccessTokenExpires = DateTime.Now.Add(new TimeSpan((respdata.expires_in - 60) * 10000000));
                }
            }
        }

        protected OAuthTicket()
        {
            this.AccessTokenLock = new object();
        }

        public OAuthTicket(string refreshToken, string clientid, string clientsecret, string redirecturi)
            : this()
        {
            this._RefreshToken = refreshToken;
            this.ClientId = clientid;
            this.ClientSecret = clientsecret;
            this.RedirectUri = redirecturi;
            RefreshAuthToken();
        }

        public static OAuthTicket FromAuthCode(string authcode, string clientid, string clientsecret, string redirecturi)
        {
            var req = HTTPHelper.CreateRequest(null, OAuthTokenRequestUri);
            var reqdata = new
            {
                code = authcode,
                client_id = clientid,
                client_secret = clientsecret,
                redirect_uri = redirecturi,
                grant_type = "authorization_code"
            };

            var respdata = HTTPHelper.ToJson(HTTPHelper.SendUrlEncodedPostData(req, reqdata));

            return new OAuthTicket
            {
                ClientId = clientid,
                ClientSecret = clientsecret,
                RedirectUri = redirecturi,
                _RefreshToken = respdata.refresh_token,
                _AccessToken = respdata.access_token,
                _TokenType = respdata.token_type,
                AccessTokenExpires = DateTime.Now.Add(new TimeSpan((respdata.expires_in - 60) * 10000000))
            };
        }
    }
}
