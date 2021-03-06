﻿using P2PChatProj.Exceptions;
using P2PChatProj.Models;
using P2PChatProj.Services;
using P2PChatProj.ViewModels.Commands;
using P2PChatProj.ViewModels.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace P2PChatProj.ViewModels
{
    public class MenuViewModel : BaseViewModel
    {
        // Private backing fields
        private string inputSearchHistory = "";
        private Visibility exitButtonVisibility = Visibility.Collapsed;
        private Visibility acceptDeclineButtonVisibility = Visibility.Collapsed;
        private ValidationError ipAddressError = new ValidationError();
        private ValidationError portNumberError = new ValidationError();

        #region Properties

        public User User { get; private set; }

        public Connection Connection { get; private set; }

        // Parent viewmodel
        public OnlineViewModel OnlineViewModel { get; private set; }

        // Commands
        public DelegateCommand SendRequestCommand { get; private set; }

        public DelegateCommand ExitButtonCommand { get; private set; }

        public DelegateCommand AcceptButtonCommand { get; private set; }

        public DelegateCommand DeclineButtonCommand { get; private set; }

        public ParameterCommand ViewChatCommand { get; private set; }

        // Menu and chat state information
        public Visibility ExitButtonVisibility 
        {
            get
            {
                return exitButtonVisibility;
            }
            set
            {
                exitButtonVisibility = value;
                RaisePropertyChanged("ExitButtonVisibility");
            }
        }

        public Visibility AcceptDeclineButtonVisibility
        {
            get
            {
                return acceptDeclineButtonVisibility;
            }
            set
            {
                acceptDeclineButtonVisibility = value;
                RaisePropertyChanged("AcceptDeclineButtonVisibility");
            }
        }

        // User input
        public string InputIpAddress { get; set; } = "";

        public string InputPortNumber { get; set; } = "";

        public string InputSearchHistory 
        { 
            get
            {
                return inputSearchHistory;
            } 
            set
            {
                inputSearchHistory = value;
                UpdateSearch();
            }
        }

        // Validation errors
        public ValidationError IpAddressError
        {
            get
            {
                return ipAddressError;
            }
            set
            {
                ipAddressError = value;
                RaisePropertyChanged("IpAddressError");
            }
        }

        public ValidationError PortNumberError
        {
            get
            {
                return portNumberError;
            }
            set
            {
                portNumberError = value;
                RaisePropertyChanged("PortNumberError");
            }
        }

        // History list
        public ObservableCollection<ChatData> ChatHistoryList { get; set; }

        public ObservableCollection<ChatData> FilteredHistoryList { get; set; }

        #endregion

        /// <summary>
        /// MenuViewModel constructor
        /// </summary>
        /// <param name="onlineViewModel">Parent viewmodel</param>
        /// <param name="user">User information</param>
        public MenuViewModel(OnlineViewModel onlineViewModel, User user, Connection connection)
        {
            User = user;
            Connection = connection;
            Connection.UpdateMenuButtons = UpdateButtons;
            OnlineViewModel = onlineViewModel;

            ChatHistoryList = new ObservableCollection<ChatData>();
            FilteredHistoryList = new ObservableCollection<ChatData>();
            Task.Run(() => LoadHistory());

            // Create new command objects
            SendRequestCommand = new DelegateCommand(SendRequest, IsDisconnected);
            ExitButtonCommand = new DelegateCommand(HandleExit);
            AcceptButtonCommand = new DelegateCommand(AcceptRequest);
            DeclineButtonCommand = new DelegateCommand(DeclineRequest);
            ViewChatCommand = new ParameterCommand(ViewChatFromHistory, IsDisconnected);
        }

        private bool IsDisconnected()
        {
            return Connection.State == ConnectionState.Listening;
        }

        private void ViewChatFromHistory(object match)
        {
            Console.WriteLine("STATUS: Getting chat from history");
            ChatData chatData = ChatHistoryList.Single(chat => chat.SearchString == (string)match);
            OnlineViewModel.LoadChatAsHistory(chatData);
        }

        private async void SendRequest()
        {
            Console.WriteLine("STATUS: Validating request input");
            bool hasInputError = ValidateInput();

            if (!hasInputError)
            {
                Console.WriteLine("RESULT: Correct input");
                await Connection.ConnectToRemoteUser(InputIpAddress, Convert.ToInt32(InputPortNumber));
            }
            else
            {
                Console.WriteLine("RESULT: Incorrect input values");
            }
        }

        private async void HandleExit()
        {
            Console.WriteLine("STATUS: Handling exit");
            await Connection.SendNetworkData(new NetworkData(User, NetworkDataType.Response, "", ResponseType.Exit));
            if (Connection.State == ConnectionState.Chatting)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OnlineViewModel.ExitChat();
                });
            }
            Connection.State = ConnectionState.Listening;
        }

        private async void DeclineRequest()
        {
            Console.WriteLine("STATUS: Declining request");
            await Connection.SendNetworkData(new NetworkData(User, NetworkDataType.Response, "", ResponseType.Decline));
            Connection.State = ConnectionState.Listening;
        }

        private async void AcceptRequest()
        {
            Console.WriteLine("STATUS: Accepting request");
            await Connection.SendNetworkData(new NetworkData(User, NetworkDataType.Response, "", ResponseType.Accept));
            Connection.State = ConnectionState.Chatting;
        }

        internal void AddChatToHistory(ChatData chatData)
        {
            ChatHistoryList.Insert(0, chatData);
            UpdateSearch();
        }

        private async Task LoadHistory()
        {
            Console.WriteLine("STATUS: Loading history");

            List<ChatData> historyList = null;

            try
            {
                historyList = await FileService.LoadHistoryAsync();
            }
            catch (HistoryNotFoundException ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
            
            
            if (historyList != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (ChatData chatData in historyList)
                    {
                        ChatHistoryList.Add(chatData);
                    }
                    UpdateSearch();
                });
                Console.WriteLine("RESULT: Chat history successfully loaded");
            }
            else
            {
                Console.WriteLine("RESULT: Chat history failed to load");
            }
        }

        private void UpdateSearch()
        {
            FilteredHistoryList.Clear();
            Console.WriteLine("STATUS: Updating search");
            if (!String.IsNullOrEmpty(InputSearchHistory))
            {
                IEnumerable<ChatData> filter = from chat in ChatHistoryList
                                               where (chat.SearchString.ToLower().Contains(InputSearchHistory.ToLower()))
                                               select chat;

                foreach(ChatData chatData in filter)
                {
                    FilteredHistoryList.Add(chatData);
                }
            }
            else
            {
                foreach (ChatData chatData in ChatHistoryList)
                {
                    FilteredHistoryList.Add(chatData);
                }
            }
        }

        private void UpdateButtons()
        {
            Console.WriteLine("STATUS: Updating menu buttons");
            if (Connection.State == ConnectionState.Waiting ||
                    Connection.State == ConnectionState.Chatting)
            {
                ExitButtonVisibility= Visibility.Visible;
                AcceptDeclineButtonVisibility = Visibility.Collapsed;
            }
            else if (Connection.State == ConnectionState.Responding)
            {
                ExitButtonVisibility = Visibility.Collapsed;
                AcceptDeclineButtonVisibility = Visibility.Visible;
            }
            else
            {
                ExitButtonVisibility = Visibility.Collapsed;
                AcceptDeclineButtonVisibility = Visibility.Collapsed;
            }
        }

        internal async void ClosingApp(object sender, CancelEventArgs e)
        {
            Console.WriteLine("STATUS: MenuViewModel closing");
            await Connection.SendNetworkData(new NetworkData(User, NetworkDataType.Response, "", ResponseType.Disconnect));
            Connection.CloseConnection();
            await FileService.WriteHistoryAsync(ChatHistoryList.ToList());
        }

        private bool ValidateInput()
        {
            bool hasError = false;

            // Removes previous error
            IpAddressError = new ValidationError();

            // Checking if ip address is empty
            if (String.IsNullOrWhiteSpace(InputIpAddress))
            {
                IpAddressError = new ValidationError()
                {
                    ErrorMessage = "Please enter an IP address",
                    HasError = Visibility.Visible
                };
                Console.WriteLine("ERROR: Empty ip address input");
                hasError = true;
            }
            // Checking validity of entered ip address
            else
            {
                try
                {
                    IPAddress.Parse(InputIpAddress);
                }
                catch (FormatException)
                {
                    IpAddressError = new ValidationError()
                    {
                        ErrorMessage = "Invalid IP address",
                        HasError = Visibility.Visible
                    };
                    Console.WriteLine("ERROR: IP address entered is invalid");
                    hasError = true;
                }
            }

            // Removes previous error
            PortNumberError = new ValidationError();

            // Checking if port number is empty
            if (String.IsNullOrWhiteSpace(InputPortNumber))
            {
                PortNumberError = new ValidationError()
                {
                    ErrorMessage = "Please enter a port number",
                    HasError = Visibility.Visible
                };
                Console.WriteLine("ERROR: Empty port number input");
                hasError = true;
            }
            else
            {
                // Checking if port number is a valid integer
                int portNumberConvert;

                try
                {
                    portNumberConvert = Convert.ToInt32(InputPortNumber);
                }
                catch (FormatException)
                {
                    PortNumberError = new ValidationError()
                    {
                        ErrorMessage = "Port number must be an integer",
                        HasError = Visibility.Visible
                    };
                    Console.WriteLine("ERROR: Port number input can't convert to integer");
                    hasError = true;
                    return hasError;
                }

                // Checking if port number is between 1024 - 65000
                if (portNumberConvert < 1024 || portNumberConvert > 65000)
                {
                    PortNumberError = new ValidationError()
                    {
                        ErrorMessage = "Only ports 1024 - 65000 can be used",
                        HasError = Visibility.Visible
                    };
                    Console.WriteLine("ERROR: Port number not between 1024 - 65000");
                    hasError = true;
                }
            }
            return hasError;
        }

       


    }
}
