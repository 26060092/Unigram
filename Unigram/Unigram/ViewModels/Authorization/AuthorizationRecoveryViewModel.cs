//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Entities;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Authorization
{
    public class AuthorizationRecoveryViewModel : TLViewModelBase
    {
        private AuthorizationStateWaitPassword _parameters;

        public AuthorizationRecoveryViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ForgotCommand = new RelayCommand(ForgotExecute);
            ResetCommand = new RelayCommand(ResetExecute);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var authState = ClientService.GetAuthorizationState();
            if (authState is AuthorizationStateWaitPassword waitPassword)
            {
                _parameters = waitPassword;
                RecoveryEmailAddressPattern = waitPassword.RecoveryEmailAddressPattern;
            }

            return Task.CompletedTask;
        }

        private string _recoveryEmailAddressPattern;
        public string RecoveryEmailAddressPattern
        {
            get => _recoveryEmailAddressPattern;
            set => Set(ref _recoveryEmailAddressPattern, value);
        }

        private string _recoveryCode;
        public string RecoveryCode
        {
            get => _recoveryCode;
            set => Set(ref _recoveryCode, value);
        }

        private bool _isResettable;
        public bool IsResettable
        {
            get => _isResettable;
            set => Set(ref _isResettable, value);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (string.IsNullOrEmpty(_recoveryCode))
            {
                RaisePropertyChanged("RECOVERY_CODE_INVALID");
                return;
            }

            var response = await ClientService.SendAsync(new RecoverAuthenticationPassword(_recoveryCode, string.Empty, string.Empty));
            if (response is Error error)
            {
                if (error.TypeEquals(ErrorType.CODE_INVALID))
                {
                    RecoveryCode = string.Empty;
                    RaisePropertyChanged("RECOVERY_CODE_INVALID");
                }
                else if (error.CodeEquals(ErrorCode.FLOOD))
                {
                    AlertsService.ShowFloodWaitAlert(error.Message);
                    //await new MessageDialog($"{Resources.FloodWaitString}\r\n\r\n({result.Error.Message})", Resources.Error).ShowAsync();
                }

                Logs.Logger.Error(Logs.LogTarget.API, "account.checkPassword error " + error);
            }
        }

        public RelayCommand ForgotCommand { get; }
        private async void ForgotExecute()
        {
            if (_parameters == null)
            {
                // TODO: ...
                return;
            }

            if (_parameters.HasRecoveryEmailAddress)
            {
                IsLoading = true;

                var response = await ClientService.SendAsync(new RequestAuthenticationPasswordRecovery());
                if (response is Error error)
                {
                    IsLoading = false;
                    await ShowPopupAsync(error.Message, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
            else
            {
                await ShowPopupAsync(Strings.Resources.RestorePasswordNoEmailText, Strings.Resources.RestorePasswordNoEmailTitle, Strings.Resources.OK);
                IsResettable = true;
            }
        }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            var confirm = await ShowPopupAsync(Strings.Resources.ResetMyAccountWarningText, Strings.Resources.ResetMyAccountWarning, Strings.Resources.ResetMyAccountWarningReset, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                IsLoading = true;

                var response = await ClientService.SendAsync(new DeleteAccount("Forgot password", string.Empty));
                if (response is Ok)
                {
                    //var logout = await LegacyService.LogOutAsync();

                    //var state = new SignUpPage.NavigationParameters
                    //{
                    //    PhoneNumber = _parameters.PhoneNumber,
                    //    PhoneCode = _parameters.PhoneCode,
                    //    Result = _parameters.Result,
                    //};

                    //NavigationService.Navigate(typeof(SignUpPage), state);
                }
                else if (response is Error error)
                {
                    IsLoading = false;

                    if (error.Message.Contains("2FA_RECENT_CONFIRM"))
                    {
                        await ShowPopupAsync(Strings.Resources.ResetAccountCancelledAlert, Strings.Resources.AppName, Strings.Resources.OK);
                    }
                    else if (error.Message.StartsWith("2FA_CONFIRM_WAIT_"))
                    {
                        // TODO: show info
                    }
                    else
                    {
                        await ShowPopupAsync(error.Message, Strings.Resources.AppName, Strings.Resources.OK);
                    }
                }
            }
        }
    }
}
