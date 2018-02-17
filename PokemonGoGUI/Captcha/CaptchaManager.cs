using System;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using PokemonGoGUI.Captcha.Anti_Captcha;
using PokemonGoGUI.UI;
using PokemonGoGUI.GoManager;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using Google.Protobuf;
using POGOProtos.Networking.Responses;
using System.Diagnostics;
using PokemonGoGUI.Enums;

namespace PokemonGoGUI.Captcha
{
    public class CaptchaManager
    {
        const string POKEMON_GO_GOOGLE_KEY = "6LeeTScTAAAAADqvhqVMhPpr_vB9D364Ia-1dSgK";

        public async Task<bool> SolveCaptcha(Client client, string captchaUrl)
        {
            bool Resolved = false;
            string captchaResponse = "";
            int retry = client.ClientManager.UserSettings.AutoCaptchaRetries;

            while (retry-- > 0 && !Resolved)
            {
                //Use captcha solution to resolve captcha
                if (!Resolved && client.ClientManager.UserSettings.EnableCaptchaSolutions &&
                    !string.IsNullOrEmpty(client.ClientManager.UserSettings.CaptchaSolutionAPIKey) &&
                    !string.IsNullOrEmpty(client.ClientManager.UserSettings.CaptchaSolutionsSecretKey))
                {
                    client.ClientManager.LogCaller(new LoggerEventArgs("Auto resolving captcha by using captcha solution service, please wait..........", LoggerTypes.Captcha));
                    CaptchaSolutionClient _client = new CaptchaSolutionClient(client.ClientManager.UserSettings.CaptchaSolutionAPIKey,
                        client.ClientManager.UserSettings.CaptchaSolutionsSecretKey, client.ClientManager.UserSettings.AutoCaptchaTimeout);
                    captchaResponse = await _client.ResolveCaptcha(client, POKEMON_GO_GOOGLE_KEY, captchaUrl);
                    if (!string.IsNullOrEmpty(captchaResponse))
                    {
                        Resolved = await Resolve(client, captchaResponse);
                    }
                }

                //use 2 captcha
                if (!Resolved && client.ClientManager.UserSettings.Enable2Captcha &&
                    !string.IsNullOrEmpty(client.ClientManager.UserSettings.TwoCaptchaAPIKey))
                {
                    if (string.IsNullOrEmpty(captchaUrl)) return true;

                    client.ClientManager.LogCaller(new LoggerEventArgs("Auto resolving captcha by using 2Captcha service", LoggerTypes.Captcha));
                    captchaResponse = await GetCaptchaResponseBy2Captcha(client, captchaUrl);
                    if (!string.IsNullOrEmpty(captchaResponse))
                    {
                        Resolved = await Resolve(client, captchaResponse);
                    }
                }

                if (!Resolved && client.ClientManager.UserSettings.EnableAntiCaptcha && !string.IsNullOrEmpty(client.ClientManager.UserSettings.AntiCaptchaAPIKey))
                {
                    if (string.IsNullOrEmpty(captchaUrl)) return true;

                    client.ClientManager.LogCaller(new LoggerEventArgs("Auto resolving captcha by using anti captcha service", LoggerTypes.Captcha));
                    captchaResponse = await GetCaptchaResponseByAntiCaptcha(client, captchaUrl);
                    if (!string.IsNullOrEmpty(captchaResponse))
                    {
                        Resolved = await Resolve(client, captchaResponse);
                    }
                }
            }

            //captchaRespose = "";
            if (!Resolved)
            {
                if (client.ClientManager.UserSettings.PlaySoundOnCaptcha)
                {
                    SystemSounds.Asterisk.Play();
                }

                captchaResponse = GetCaptchaResponseManually(client, captchaUrl);
                //captchaResponse = await GetCaptchaTokenWithInternalForm(captchaUrl);

                if (!string.IsNullOrEmpty(captchaResponse))
                {
                    Resolved = await Resolve(client, captchaResponse);
                }
            }
            return Resolved;
        }

        //NOT WORKING  SINCE WEB BROWSER CONTROL IS IE7, doesn't work with captcha page
        private async Task<string> GetCaptchaTokenWithInternalForm(string captchaUrl)
        {
            string response = "";
            var t = new Thread(() =>
            {
                CaptchaSolveForm captcha = new CaptchaSolveForm(captchaUrl);
                if (captcha.ShowDialog() == DialogResult.OK)
                {
                    response = "Aaa";
                }

                //captcha.TopMost = true;
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            int count = 120;
            while (true && count > 0)
            {
                count--;
                //Thread.Sleep(1000);
                await Task.Delay(1000);
            }

            return response;
        }

        private async Task<bool> Resolve(Client client, string captchaResponse)
        {
            if (string.IsNullOrEmpty(captchaResponse)) return false;

            /*if (!client.LoggedIn)
            {
                MethodResult result = await client.ClientManager.AcLogin();

                if (!result.Success)
                {
                    return result.Success;
                }
            }*/

            var response = await client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.VerifyChallenge,
                RequestMessage = new VerifyChallengeMessage
                {
                    Token = captchaResponse
                }.ToByteString()
            }, false, false, false);

            if (response == null)
                await Resolve(client, captchaResponse);
            else
            {
                var verifyChallengeResponse = VerifyChallengeResponse.Parser.ParseFrom(response);

                if (!verifyChallengeResponse.Success)
                {
                    client.ClientManager.LogCaller(new LoggerEventArgs($"(CAPTCHA) Failed to resolve captcha, try resolved captcha by official app. ", LoggerTypes.Warning));
                    return false;
                }

                //TODO: May be this have bug...
                //if (client.ClientSession.State == SessionState.Paused)
                //    await client.ClientSession.ResumeAsync();
                //

                client.ClientManager.LogCaller(new LoggerEventArgs($"(CAPTCHA) Great!!! Captcha has been by passed", LoggerTypes.Success));
                return verifyChallengeResponse.Success;
            }

            return false;
        }

        private async Task<string> GetCaptchaResponseByAntiCaptcha(Client client, string captchaUrl)
        {
            bool solved = false;
            int retries = client.ClientManager.UserSettings.AutoCaptchaRetries;
            string result = null;

            while (retries-- > 0 && !solved)
            {
                result = await AntiCaptchaClient.SolveCaptcha(client, captchaUrl,
                    client.ClientManager.UserSettings.AntiCaptchaAPIKey,
                    POKEMON_GO_GOOGLE_KEY,
                    client.ClientManager.UserSettings.ProxyHostCaptcha.ToString(),
                    client.ClientManager.UserSettings.ProxyPortCaptcha);
                solved = !string.IsNullOrEmpty(result);
            }
            if (solved)
            {
                client.ClientManager.LogCaller(new LoggerEventArgs("Captcha has been resolved automatically by Anti-Captcha ", LoggerTypes.Success));
                return result;
            }
            return String.Empty;
        }

        private async Task<string> GetCaptchaResponseBy2Captcha(Client client, string captchaUrl)
        {
            bool solved = false;
            int retries = client.ClientManager.UserSettings.AutoCaptchaRetries;
            string result = null;

            while (retries-- > 0 && !solved)
            {
                TwoCaptchaClient _client = new TwoCaptchaClient(client.ClientManager.UserSettings.TwoCaptchaAPIKey);

                result = await _client.SolveRecaptchaV2(client, POKEMON_GO_GOOGLE_KEY, captchaUrl, string.Empty, ProxyType.HTTP);
                solved = !string.IsNullOrEmpty(result);
            }
            if (solved)
            {
                client.ClientManager.LogCaller(new LoggerEventArgs("Captcha has been resolved automatically by 2Captcha ", LoggerTypes.Success));
                return result;
            }
            return String.Empty;
        }

        public string GetCaptchaResponseManually(Client client, string url)
        {
            if (!client.ClientManager.UserSettings.AllowManualCaptchaResolve) return null;

            if (!File.Exists("chromedriver.exe"))
            {
                client.ClientManager.LogCaller(new LoggerEventArgs(
                     $"You enable manual captcha resolve but bot didn't setup files properly, please download chromedriver.exe put in same folder.",
                    LoggerTypes.FatalError));
                return null;
            }
            IWebDriver webDriver = null;
            try
            {
                webDriver = new ChromeDriver(Environment.CurrentDirectory, new ChromeOptions()
                {
                    //
                });

                webDriver.Navigate().GoToUrl(url);
                client.ClientManager.LogCaller(new LoggerEventArgs($"Captcha is being show in separate thread window, please check your chrome browser and resolve it before {client.ClientManager.UserSettings.ManualCaptchaTimeout} seconds", LoggerTypes.Captcha));

                var wait = new WebDriverWait(
                    webDriver,
                    TimeSpan.FromSeconds(client.ClientManager.UserSettings.ManualCaptchaTimeout)
                );
                //wait.Until(ExpectedConditions.ElementIsVisible(By.Id("recaptcha-verify-button")));
                wait.Until(d =>
                 {
                     var ele = d.FindElement(By.Id("g-recaptcha-response"));

                     if (ele == null) return false;
                     return ele.GetAttribute("value").Length > 0;
                 });

                string token = wait.Until<string>(driver =>
                {
                    var ele = driver.FindElement(By.Id("g-recaptcha-response"));
                    string t = ele.GetAttribute("value");
                    return t;
                });

                return token;
            }
            catch (Exception ex)
            {
                client.ClientManager.LogCaller(new LoggerEventArgs($"You didn't resolve captcha in the given time", LoggerTypes.Exception, ex));
                return String.Empty;
            }
            finally
            {
                if (webDriver != null) webDriver.Close();
                //this closes all cmd window but if more chaptas is in runs not good..
                //foreach (var process in Process.GetProcessesByName("chromedriver"))
                //{
                //    process.Kill();
                //}
            }
        }
    }
}