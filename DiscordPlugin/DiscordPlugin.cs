using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Sandbox;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.Plugins;
using VRageMath;

namespace DiscordPlugin
{
    public class DiscordPlugin : IPlugin
    {
        private bool _init;
        private bool _unitTests;
        private static Type _terminalChatType;
        public static Dictionary<MyGuiControlListbox.Item, List<RichTextLabel>> ChatHistories { get; set; } = new Dictionary<MyGuiControlListbox.Item, List<RichTextLabel>>();
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
        private MyGuiControlListbox.Item _selectedItem;
        private MyGuiControlListbox.Item _lastSelectedItem;
        private readonly StringBuilder _currentText = new StringBuilder();
        private readonly StringBuilder _lastText = new StringBuilder();
        private bool _currentFocus;
        private bool _lastFocus;

        [Flags]
        private enum UpdateFlags
        {
            ItemsSelected,
            BoxFocus,
            BoxChange,
            BoxEnter,
            ButtonPress
        }

        public class RichTextLabel
        {
            public RichTextLabel( string from, string message, MyFontEnum fromFont, string messageFont = MyFontEnum.White )
            {
                From = from;
                Message = message;
                FromFont = fromFont;
                MessageFont = messageFont;
            }
            public string From;
            public MyFontEnum FromFont;
            public string Message;
            public MyFontEnum MessageFont;
        }

        public void Dispose()
        {
            //Login.Instance.loginState = Login.LoginState.Username;
            //LoginPrompt= new MyGuiControlListbox.Item(new StringBuilder("Login to the SE Discord"), null, null, MySession.Static.LocalHumanPlayer.Identity);
            //ChatHistories.Clear();
        }

        public void Init( object gameInstance )
        {
            MySandboxGame.Log.WriteLineAndConsole($"##Discord Plugin: Initializing v{Version}" );
            MySandboxGame.Log.WriteLine("##Discord Plugin: If something goes wrong, it was Tim's fault.");

            if ( !ReflectionUnitTests() )
            {
                MySandboxGame.Log.WriteLineAndConsole( "##Discord Plugin: Failed reflection unit tests!" );
                _unitTests = false;
                return;
            }
            _unitTests = true;
        }
        

        public void Update()
        {
            if ( !_unitTests || MySession.Static?.LocalHumanPlayer == null )
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

            if ( m_playerList.SelectedItems.Count > 0 && ChatHistories.Keys.Contains( m_playerList.SelectedItems.First() ) )
            {
               m_chatHistory.Clear();
                foreach ( var text in ChatHistories[m_playerList.SelectedItems.First()] )
                {
                    m_chatHistory.AppendText( text.From, text.FromFont, m_chatHistory.TextScale, Vector4.One );
                    m_chatHistory.AppendText( text.Message, text.MessageFont, m_chatHistory.TextScale, Vector4.One );
                    m_chatHistory.AppendLine();
                }
            }
        }

        private bool ReflectionUnitTests()
        {
            _terminalChatType = typeof(MyGuiScreenTerminal).GetField( "m_controllerChat", BindingFlags.NonPublic | BindingFlags.Instance )?.FieldType;
            if ( _terminalChatType == null )
            {
                MySandboxGame.Log.WriteLineAndConsole( "##Discord Plugin: Failed to get terminal chat type" );
                return false;
            }

            FieldInfo[] fields = _terminalChatType.GetFields( BindingFlags.NonPublic | BindingFlags.Instance );

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
            if ( _updateFlags.HasFlag( UpdateFlags.ItemsSelected ) && _selectedItem == null )
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

                if ( _lastSelectedItem != null )
                {
                    m_playerList.SelectedItems.Add( _lastSelectedItem );
                    _selectedItem = _lastSelectedItem;
                }

                if ( !_currentFocus && _lastFocus )
                {
                    _currentFocus = true;
                    _terminalInstance.FocusedControl = m_chatbox;
                }

                if ( !_updateFlags.HasFlag( UpdateFlags.BoxEnter ) && _updateFlags.HasFlag( UpdateFlags.BoxChange ) )
                {
                        m_chatbox.SetText( _lastText );
                        m_chatbox.MoveCarriageToEnd();
                        _currentText.Clear();
                        _currentText.Append( _lastText );
                }

            }
            _updateFlags = 0;
        }

        public static void FormatMessage( string from, string message, ref MyGuiControlMultilineText history )
        {
            MyFontEnum fromFont = MyFontEnum.DarkBlue;
            if(from.Equals( Login.Instance.Username, StringComparison.CurrentCultureIgnoreCase ))
                fromFont=MyFontEnum.White;

            history.AppendText( $"{from}: ", fromFont, m_chatHistory.TextScale, Vector4.One );
            history.AppendText( message, MyFontEnum.White, m_chatHistory.TextScale, Vector4.One );
            history.AppendLine();
        }

        public Version Version
        {
            get { return typeof(DiscordPlugin).Assembly.GetName().Version; }
        }

        #region Event Handlers

        private void M_playerList_ItemsSelected( MyGuiControlListbox obj )
        {
            _updateFlags |= UpdateFlags.ItemsSelected;
            _lastSelectedItem = _selectedItem;
            _selectedItem = m_playerList.SelectedItems.FirstOrDefault();
        }

        private void M_sendButton_ButtonClicked( MyGuiControlButton obj )
        {
            _updateFlags |= UpdateFlags.ButtonPress;
            m_chatbox.Type = MyGuiControlTextboxType.Normal;
            if ( Login.Instance.LoginStep != Login.LoginState.Confirmed )
            {
                Login.Instance.ProcessLogin( _lastText.ToString() );
            }
        }

        private void M_chatbox_EnterPressed( MyGuiControlTextbox obj )
        {
            _updateFlags |= UpdateFlags.BoxEnter;
            obj.Type = MyGuiControlTextboxType.Normal;
            if ( Login.Instance.LoginStep != Login.LoginState.Confirmed )
            {
                Login.Instance.ProcessLogin(_lastText.ToString());
            }
        }

        private void M_chatbox_FocusChanged( MyGuiControlBase arg1, bool arg2 )
        {
            _updateFlags |= UpdateFlags.BoxFocus;
            _lastFocus = _currentFocus;
            _currentFocus = arg2;
        }
        
        private void M_chatbox_TextChanged(MyGuiControlTextbox obj)
        {
            //apparently this event can fire multiple times in an update
            if ( _updateFlags.HasFlag( UpdateFlags.BoxChange ) )
                return;

            _updateFlags|= UpdateFlags.BoxChange;
            _lastText.Clear();
            _lastText.Append( _currentText );
            _currentText.Clear();
            obj.GetText(_currentText);
        }

        #endregion
    }
}
