using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.Library.Collections;

namespace DiscordPlugin
{
    public class Login
    {
        public enum LoginState
        {
            Token,
            //Password,
            InvalidLogin,
            Config,
            Confirmed
        }

        private static Login _instance;

        public MyGuiControlListbox.Item LoginPrompt = new MyGuiControlListbox.Item(new StringBuilder("= Login to Discord ="), null, null, MySession.Static.LocalHumanPlayer.Identity);
        public LoginState LoginStep = LoginState.Token;
        public string Username;
        private string _token;

        public static Login Instance
        {
            get
            {
                if ( _instance == null )
                    _instance = new Login();
                return _instance;
            }
        }

        public void DisplayLogin()
        {
            if ( !DiscordPlugin.m_playerList.SelectedItems.Contains( LoginPrompt ) )
                return;

            switch ( LoginStep )
            {
                case LoginState.Token:
                    DiscordPlugin.m_chatHistory.Clear();
                    DiscordPlugin.m_chatHistory.AppendText( "Please enter your Discord Token." );
                    break;

                //case LoginState.Password:
                //    DiscordPlugin.m_chatHistory.Clear();
                //    DiscordPlugin.m_chatHistory.AppendText( "Please enter your password." );
                //    DiscordPlugin.m_chatbox.Type = MyGuiControlTextboxType.Password;
                //    break;

                case LoginState.InvalidLogin:
                    //DiscordPlugin.m_chatHistory.Clear();
                    //DiscordPlugin.m_chatHistory.AppendText( "Invalid login details, try again." );
                    //DiscordPlugin.m_chatHistory.AppendLine();
                    //DiscordPlugin.m_chatHistory.AppendText( "Please enter your Discord token." );
                    break;

                case LoginState.Config:
                    DiscordPlugin.m_chatHistory.Clear();
                    if ( DiscordManager.Instance.Servers == null )
                    {
                        DiscordPlugin.m_chatHistory.AppendText( "Select the servers you want to see. Use the number to the left of the server name, separate multiple values with a comma." );
                        DiscordPlugin.m_chatHistory.AppendLine();
                        DiscordPlugin.m_chatHistory.AppendText( DiscordManager.Instance.ServerList() );
                    }
                    else if ( DiscordManager.Instance.Channels == null )
                    {
                        DiscordPlugin.m_chatHistory.AppendText( "Select the channels you want to see." );
                        DiscordPlugin.m_chatHistory.AppendLine();
                        DiscordPlugin.m_chatHistory.AppendText( DiscordManager.Instance.ChannelList() );
                    }
                    else
                    {
                        DiscordPlugin.m_chatHistory.AppendText( "Do you want to see a notificaion for [0] @Mentions, [1] Private Messages, [2] both, or [3] none?" );
                    }
                    break;
            }
        }

        public void ProcessLogin( string chatEntry )
        {
            switch ( LoginStep )
            {
                case LoginState.InvalidLogin:
                case LoginState.Token:
                    _token = chatEntry;
                    //LoginStep = LoginState.Config;
                    //break;
                
                //case LoginState.Password:
                    switch (DiscordManager.Instance.Login(_token).Result)
                    {
                        case DiscordManager.LoginResult.Ok:
                            LoginStep = LoginState.Config;
                            DiscordPlugin.m_chatHistory.Clear();
                            DiscordPlugin.m_chatHistory.AppendText("Login successful! Loading Discord...");
                            break;
                        case DiscordManager.LoginResult.Invalid:
                            LoginStep=LoginState.InvalidLogin;
                            DiscordPlugin.m_chatHistory.Clear();
                            DiscordPlugin.m_chatHistory.AppendText("Invalid login details, try again.");
                            DiscordPlugin.m_chatHistory.AppendLine();
                            DiscordPlugin.m_chatHistory.AppendText("Please enter your Discord token.");
                            break;
                        case DiscordManager.LoginResult.RateLimit:
                            LoginStep = LoginState.InvalidLogin;
                            DiscordPlugin.m_chatHistory.Clear();
                            DiscordPlugin.m_chatHistory.AppendText("You have been rate limited by Discord, try again later.");
                            break;
                        case DiscordManager.LoginResult.Error:
                            LoginStep = LoginState.InvalidLogin;
                            DiscordPlugin.m_chatHistory.Clear();
                            DiscordPlugin.m_chatHistory.AppendText("Unspecified error during login. Check the log.");
                            DiscordPlugin.m_chatHistory.AppendLine();
                            DiscordPlugin.m_chatHistory.AppendText("Please enter your Discord token.");
                            break;
                        case DiscordManager.LoginResult.None:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;

                    case  LoginState.Config:
                    if ( DiscordManager.Instance.Servers == null )
                    {
                        var splits = chatEntry.Split( ',' );
                        List<int> indicies = new List<int>();
                        foreach ( var split in splits )
                        {
                            int index;
                            if ( !int.TryParse( split, out index ) )
                                continue;
                            indicies.Add( index );
                        }
                        DiscordManager.Instance.PopulateServers( indicies.ToArray() );
                    }
                    else if ( DiscordManager.Instance.Channels == null )
                    {
                        var splits = chatEntry.Split(',');
                        List<int> indicies = new List<int>();
                        foreach ( var split in splits )
                        {
                            int index;
                            if (!int.TryParse(split, out index))
                                continue;
                            indicies.Add(index);
                        }
                        DiscordManager.Instance.PopulateChannels(indicies.ToArray());
                    }
                    else
                    {
                        int mode;
                        if ( int.TryParse( chatEntry, out mode ) )
                            DiscordManager.Instance.NotificationMode = (DiscordManager.NotificationModeEnum)mode;
                    }
                    break;
            }
        }
    }
}