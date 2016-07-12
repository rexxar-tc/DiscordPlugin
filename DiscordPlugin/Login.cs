using System.Text;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;

namespace DiscordPlugin
{
    public class Login
    {
        public enum LoginState
        {
            Username,
            Password,
            InvalidLogin,
            Config,
            Confirmed
        }

        private static Login _instance;

        public MyGuiControlListbox.Item LoginPrompt = new MyGuiControlListbox.Item(new StringBuilder("= Login to the SE Discord ="), null, null, MySession.Static.LocalHumanPlayer.Identity);
        public LoginState LoginStep = LoginState.Username;
        public string Username;

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
            if ( !Plugin.m_playerList.SelectedItems.Contains( LoginPrompt ) )
                return;

            switch ( LoginStep )
            {
                case LoginState.Username:
                {
                    Plugin.m_chatHistory.Clear();
                    Plugin.m_chatHistory.AppendText( "Please enter your Discord username." );
                    break;
                }
                case LoginState.Password:
                {
                    Plugin.m_chatHistory.Clear();
                    Plugin.m_chatHistory.AppendText( "Please enter your password." );
                    Plugin.m_chatbox.Type = MyGuiControlTextboxType.Password;
                    break;
                }
                case LoginState.InvalidLogin:
                {
                    Plugin.m_chatHistory.Clear();
                    Plugin.m_chatHistory.AppendText( "Invalid login details, try again." );
                    Plugin.m_chatHistory.AppendLine();
                    Plugin.m_chatHistory.AppendText( "Please enter your Discord username." );
                    break;
                }
            }
        }

        public void ProcessLogin( string chatEntry )
        {
            switch ( LoginStep )
            {
                case LoginState.Username:
                {
                    //TODO: DISCORD STUFF
                    LoginStep = LoginState.Password;
                    break;
                }
                case LoginState.Password:
                {
                    //TODO: DISCORD STUFF
                    //TODO: Config menu?
                    LoginStep = LoginState.Confirmed;
                    Plugin.m_chatHistory.Clear();
                    Plugin.m_chatHistory.AppendText( "Login successful! Loading Discord channels..." );
                    //TODO: put a short delay so users can read the message
                    break;
                }
            }
        }
    }
}