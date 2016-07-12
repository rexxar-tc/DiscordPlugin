using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Sandbox;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.Plugins;

namespace DiscordPlugin
{
    public sealed class Plugin : IPlugin
    {
        private bool _init;
        private bool _discordInit;

        public static Dictionary<MyGuiControlListbox.Item, List<string[]>> ChatHistories { get; set; }
        public static MyGuiControlListbox m_playerList;
        public static MyGuiControlTextbox m_chatbox;
        public static MyGuiControlButton m_sendButton;
        public static MyGuiControlMultilineText m_chatHistory;

        private UpdateFlags _updateFlags;
        /*
         * So when the game updates the internal chat history lists, it resets chatbox/button focus,
         * clears the current chatbox contents, and resets the current selected item. We can't stop the update,
         * so we just have to buffer *everything* and restore things when we detect that the game has
         * screwed with our stuff.
         * It's a hack, yes, but there's no other option.
         */
        private MyGuiControlListbox.Item SelectedItem;
        private MyGuiControlListbox.Item LastSelectedItem;
        private StringBuilder CurrentText = new StringBuilder();
        private StringBuilder LastText = new StringBuilder();
        private bool CurrentFocus;
        private bool LastFocus;

        [Flags]
        private enum UpdateFlags
        {
            ItemsSelected,
            BoxFocus,
            BoxChange,
            BoxEnter,
            ButtonPress
        }

        public void Dispose()
        {
            //Login.Instance.loginState = Login.LoginState.Username;
            //LoginPrompt= new MyGuiControlListbox.Item(new StringBuilder("Login to the SE Discord"), null, null, MySession.Static.LocalHumanPlayer.Identity);
            //ChatHistories.Clear();
        }

        public void Init( object gameInstance )
        {
            MySandboxGame.Log.WriteLineAndConsole($"##Discord Plugin: Initializing Discord plugin v{Version}" );
            MySandboxGame.Log.WriteLine("##Discord Plugin: If something goes wrong, it was Tim's fault.");

            if ( !ReflectionUnitTests() )
            {
                MySandboxGame.Log.WriteLineAndConsole( "##Discord Plugin: Failed reflection unit tests!" );
                //don't initialize the bot. without _discordInit set, the plugin is effectively disabled
                return;
            }

            ChatHistories=new Dictionary<MyGuiControlListbox.Item, List<string[]>>();

            //TODO: Initialize discordAPI here. JOHN THIS MEANS YOU
            _discordInit = true;
        }

        public void Update()
        {
            if ( !_discordInit || MySession.Static?.LocalHumanPlayer == null )
                return;

            if ( MyGuiScreenTerminal.GetCurrentScreen() == MyTerminalPageEnum.None )
            {
                //the terminal instance is completely disposed when the player closes the scren
                //so we have to refresh it every time the screen opens
                _init = false;
                return;
            }

            if ( !_init )
            {
                if ( !TryInitTerminal() )
                {
                    MySandboxGame.Log.WriteLineAndConsole( "##Discord Plugin: Failed to init terminal!" );
                    return;
                }
                _init = true;
            }

            ProcessUpdateFlags();

            if ( Login.Instance.LoginStep != Login.LoginState.Confirmed )
            {
                Login.Instance.DisplayLogin();
                return;
            }

        }

        private bool ReflectionUnitTests()
        {
            Type terminalChatType = typeof(MyGuiScreenTerminal).GetField( "m_controllerChat", BindingFlags.NonPublic | BindingFlags.Instance )?.FieldType;
            if ( terminalChatType == null )
            {
                MySandboxGame.Log.WriteLineAndConsole( "##Discord Plugin: Failed to get terminal chat type" );
                return false;
            }

            FieldInfo[] fields = terminalChatType.GetFields( BindingFlags.NonPublic | BindingFlags.Instance );

            if ( !fields.Any( x => x.Name == "m_playerList" ) )
                return false;
            if ( !fields.Any( x => x.Name == "m_chatbox" ) )
                return false;
            if ( !fields.Any( x => x.Name == "m_sendButton" ) )
                return false;
            if ( !fields.Any( x => x.Name == "m_chatHistory" ) )
                return false;

            return true;
        }

        private MyGuiScreenTerminal _terminalInstance;

        public bool TryInitTerminal()
        {

            if ( _terminalInstance == null )
                _terminalInstance = typeof(MyGuiScreenTerminal).GetField( "m_instance", BindingFlags.NonPublic | BindingFlags.Static )?.GetValue( null ) as MyGuiScreenTerminal;

            object instance = typeof(MyGuiScreenTerminal).GetField( "m_controllerChat", BindingFlags.NonPublic | BindingFlags.Instance )?.GetValue( _terminalInstance );
            Type terminalChatType = instance?.GetType();
            if ( terminalChatType == null )
                return false;

            m_playerList = terminalChatType.GetField( "m_playerList", BindingFlags.NonPublic | BindingFlags.Instance )?.GetValue( instance ) as MyGuiControlListbox;
            if ( m_playerList == null )
                return false;

            m_chatbox = terminalChatType.GetField( "m_chatbox", BindingFlags.NonPublic | BindingFlags.Instance )?.GetValue( instance ) as MyGuiControlTextbox;
            if ( m_chatbox == null )
                return false;

            m_sendButton = terminalChatType.GetField( "m_sendButton", BindingFlags.NonPublic | BindingFlags.Instance )?.GetValue( instance ) as MyGuiControlButton;
            if ( m_sendButton == null )
                return false;

            m_chatHistory = terminalChatType.GetField( "m_chatHistory", BindingFlags.NonPublic | BindingFlags.Instance )?.GetValue( instance ) as MyGuiControlMultilineText;

            if ( m_chatHistory == null )
                return false;

            m_sendButton.ButtonClicked += M_sendButton_ButtonClicked;
            m_chatbox.EnterPressed += M_chatbox_EnterPressed;
            m_playerList.ItemsSelected += M_playerList_ItemsSelected;
            m_chatbox.FocusChanged += M_chatbox_FocusChanged;
            m_chatbox.TextChanged += M_chatbox_TextChanged;

            return true;
        }

        public void ProcessUpdateFlags()
        {
            if ( _updateFlags.HasFlag( UpdateFlags.ItemsSelected ) && SelectedItem == null )
            {
                //the game has cleared our stuff out of the list, so put it back in and reset focus and restore text and all that
                if ( Login.Instance.LoginStep != Login.LoginState.Confirmed )
                {
                    if ( !m_playerList.Items.Contains( Login.Instance.LoginPrompt ) )
                        m_playerList.Add( Login.Instance.LoginPrompt, 1 );
                }
                else if ( ChatHistories.Count > 0 && !m_playerList.Items.Contains( ChatHistories.Keys.First() ) )
                {
                    for ( int i = 0; i < ChatHistories.Count; i++ )
                    {
                        m_playerList.Add( ChatHistories.Keys.ElementAt( i ), i + 1 );
                    }
                }
                else
                {
                    return;
                }

                if ( LastSelectedItem != null )
                {
                    m_playerList.SelectedItems.Add( LastSelectedItem );
                    SelectedItem = LastSelectedItem;
                }

                if ( !CurrentFocus && LastFocus )
                {
                    CurrentFocus = true;
                    _terminalInstance.FocusedControl = m_chatbox;
                }

                if ( !_updateFlags.HasFlag( UpdateFlags.BoxEnter ) && _updateFlags.HasFlag( UpdateFlags.BoxChange ) )
                {
                        m_chatbox.SetText( LastText );
                        m_chatbox.MoveCarriageToEnd();
                        CurrentText.Clear();
                        CurrentText.Append( LastText );
                }

            }
            _updateFlags = 0;
        }

        public Version Version
        {
            get { return typeof(Plugin).Assembly.GetName().Version; }
        }

        #region Event Handlers

        private void M_playerList_ItemsSelected( MyGuiControlListbox obj )
        {
            _updateFlags |= UpdateFlags.ItemsSelected;
            LastSelectedItem = SelectedItem;
            SelectedItem = m_playerList.SelectedItems.FirstOrDefault();
        }

        private void M_sendButton_ButtonClicked( MyGuiControlButton obj )
        {
            _updateFlags |= UpdateFlags.ButtonPress;
            m_chatbox.Type = MyGuiControlTextboxType.Normal;
            if ( Login.Instance.LoginStep != Login.LoginState.Confirmed )
            {
                Login.Instance.ProcessLogin( m_chatbox.Text );
            }
        }

        private void M_chatbox_EnterPressed( MyGuiControlTextbox obj )
        {
            _updateFlags |= UpdateFlags.BoxEnter;
            obj.Type = MyGuiControlTextboxType.Normal;
            if ( Login.Instance.LoginStep != Login.LoginState.Confirmed )
            {
                Login.Instance.ProcessLogin( obj.Text );
            }
        }

        private void M_chatbox_FocusChanged( MyGuiControlBase arg1, bool arg2 )
        {
            _updateFlags |= UpdateFlags.BoxFocus;
            LastFocus = CurrentFocus;
            CurrentFocus = arg2;
        }
        
        private void M_chatbox_TextChanged(MyGuiControlTextbox obj)
        {
            if ( _updateFlags.HasFlag( UpdateFlags.BoxChange ) )
                return;

            _updateFlags|= UpdateFlags.BoxChange;
            LastText.Clear();
            LastText.Append( CurrentText );
            CurrentText.Clear();
            obj.GetText(CurrentText);
        }

        #endregion
    }
}
