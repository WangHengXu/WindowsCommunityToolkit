﻿// ******************************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
//
// ******************************************************************

namespace Microsoft.Toolkit.Uwp.Services.MicrosoftGraph
{
    using System;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Graph;
    using Microsoft.Toolkit.Uwp.Services.AzureAD;

    /// <summary>
    ///  Class for connecting to Office 365 Microsoft Graph
    /// </summary>
    public partial class MicrosoftGraphService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftGraphService"/> class.
        /// </summary>
        public MicrosoftGraphService()
        {
        }
        /// <summary>
        /// Store an instance of the MicrosoftGraphAuthenticationHelper class
        /// </summary>
        MicrosoftGraphAuthenticationHelper authentication;

        /// <summary>
        /// Private singleton field.
        /// </summary>
        private static MicrosoftGraphService instance;

        /// <summary>
        /// Store a reference to an instance of the underlying data provider.
        /// </summary>
        private GraphServiceClient graphClientProvider;

        /// <summary>
        /// Gets public singleton property.
        /// </summary>
        public static MicrosoftGraphService Instance => instance ?? (instance = new MicrosoftGraphService());

        /// <summary>
        /// Field for tracking initialization status.
        /// </summary>
        private bool isInitialized;

        /// <summary>
        /// Field for tracking if the user is connected.
        /// </summary>
        private bool isConnected;

        /// <summary>
        /// Field to store Azure AD Application clientid
        /// </summary>
        private string appClientId;

        /// <summary>
        /// Fields to store a MicrosoftGraphServiceMessages instance
        /// </summary>
        private MicrosoftGraphUserService user;

        /// <summary>
        /// Gets a reference to an instance of the MicrosoftGraphUserService class
        /// </summary>
        public MicrosoftGraphUserService User
        {
            get { return user; }
        }

        /// <summary>
        /// Initialize Microsoft Graph.
        /// </summary>
        /// <param name='appClientId'>Azure AD's App client id</param>
        /// <returns>Success or failure.</returns>
        public bool Initialize(string appClientId)
        {
            if (string.IsNullOrEmpty(appClientId))
            {
                throw new ArgumentNullException(nameof(appClientId));
            }

            this.appClientId = appClientId;

            graphClientProvider = CreateGraphClient(appClientId);
            isInitialized = true;
            return true;
        }

        /// <summary>
        /// Logout the current user
        /// </summary>
        /// <returns>success or failure</returns>
        public bool Logout()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Microsoft Graph not initialized.");
            }

            authentication = null;

            return true; // MicrosoftGraphAuthenticationHelper.Instance.LogoutAsync();
        }

        /// <summary>
        /// Login the user from Azure AD and Get Microsoft Graph access token.
        /// </summary>
        /// <remarks>Need Sign in and read user profile scopes (User.Read)</remarks>
        /// <see cref='Http://graph.microsoft.io/en-us/docs/authorization/permission_scopes'/>
        /// <returns>Returns success or failure of login attempt.</returns>
        public async Task<bool> LoginAsync()
        {
            isConnected = false;
            if (!isInitialized)
            {
                throw new InvalidOperationException("Microsoft Graph not initialized.");
            }

            authentication = new MicrosoftGraphAuthenticationHelper();
            var accessToken = await authentication.GetUserTokenAsync(appClientId);
            if (string.IsNullOrEmpty(accessToken))
            {
                return isConnected;
            }

            isConnected = true;

            await InitializeUserAsync();

            return isConnected;
        }

        /// <summary>
        /// Create Microsoft Graph client
        /// </summary>
        /// <param name='appClientId'>Azure AD's App client id</param>
        /// <returns>instance of the GraphServiceclient</returns>
        private GraphServiceClient CreateGraphClient(string appClientId)
        {
            return new GraphServiceClient(
                  new DelegateAuthenticationProvider(
                     async (requestMessage) =>
                     {
                         // requestMessage.Headers.Add('outlook.timezone', 'Romance Standard Time');
                         requestMessage.Headers.Authorization =
                                            new AuthenticationHeaderValue(
                                                     "bearer",
                                                     await authentication.GetUserTokenAsync(appClientId).ConfigureAwait(false));
                         return;
                     }));
        }

        /// <summary>
        /// Initialize a instance of MicrosoftGraphUserService class
        /// </summary>
        /// <returns><see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task InitializeUserAsync()
        {
            MicrosoftGraphUserFields[] selectedFields =
            {
                MicrosoftGraphUserFields.Id
            };
            user = new MicrosoftGraphUserService(graphClientProvider);
            await user.GetProfileAsync(selectedFields);
        }
    }
}
