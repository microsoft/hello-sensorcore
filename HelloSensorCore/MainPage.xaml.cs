/*	
The MIT License (MIT)
Copyright (c) 2015 Microsoft

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. 
 */
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;
using Lumia.Sense;
using System.Threading.Tasks;
using Windows.Web;
using Lumia.Sense.Testing;

/// <summary>
/// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556
/// </summar
namespace HelloSensorCore
{
    /// <summary>
    /// Main page for the HelloSensorCore application
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Private members
        /// <summary>
        /// Step counter instance
        /// </summary>
        private IStepCounter _stepCounter;
        #endregion

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;

            Window.Current.VisibilityChanged += async ( oo, ee ) =>
            {
                if( ee.Visible )
                {
                    if( await CallSensorcoreApiAsync( async () =>
                    {
                        if( _stepCounter == null )
                        {
                            // Get sensor instance if needed...
                            _stepCounter = await StepCounter.GetDefaultAsync();
                        }
                        else
                        {
                            // ... otherwise just activate it
                            await _stepCounter.ActivateAsync();
                        }
                    } ) )
                    {
                        // Display current reading whenever application is brought to foreground
                        await ShowCurrentReading();
                    }
                }
                else
                {
                    // Sensor needs to be deactivated when application is put to background
                    if( _stepCounter != null ) await CallSensorcoreApiAsync( async () => await _stepCounter.DeactivateAsync() );
                }
            };
        }

         /// <summary>
        /// Check motion data settings
        /// </summary>
        private async void CheckMotionDataSettings()
        {
            if (!(await StepCounter.IsSupportedAsync()))
            {
                MessageDialog dlg = new MessageDialog("Unfortunately this device does not support step counting");
                await dlg.ShowAsync();
                Application.Current.Exit();
            }
            else 
            {
                // MotionDataSettings settings = await SenseHelper.GetSettingsAsync();
                // Starting from version 2 of Motion data settings Step counter and Acitivity monitor are always available. In earlier versions system
                // location setting and Motion data had to be enabled.
                uint apiSet = await SenseHelper.GetSupportedApiSetAsync();
                MotionDataSettings settings = await SenseHelper.GetSettingsAsync();
                if (apiSet > 2)
                {
                    if (!settings.LocationEnabled)
                    {
                        MessageDialog dlg = new MessageDialog("In order to count steps you need to enable location in system settings. Do you want to open settings now?", "Information");
                        dlg.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(async (cmd) => await SenseHelper.LaunchLocationSettingsAsync())));
                        dlg.Commands.Add(new UICommand("No"));
                        await dlg.ShowAsync();
                    }
                    if (!settings.PlacesVisited)
                    {
                        MessageDialog dlg = null;
                        if (settings.Version < 2)
                        {
                            dlg = new MessageDialog("In order to count steps you need to enable Motion data collection in Motion data settings. Do you want to open settings now?", "Information");
                        }
                        else
                        {
                            dlg = new MessageDialog("In order to collect and view visited places you need to enable Places visited in Motion data settings. Do you want to open settings now? if no, application will exit", "Information");
                        }
                        dlg.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(async (cmd) => await SenseHelper.LaunchSenseSettingsAsync())));
                        dlg.Commands.Add(new UICommand("No", new UICommandInvokedHandler((cmd) => { Application.Current.Exit(); })));
                        await dlg.ShowAsync();
                    }
                }
            }
        }

        /// <summary> 
        /// Performs asynchronous SensorCore SDK operation and handles any exceptions 
        /// </summary> 
        /// <param name="action"></param> 
        /// <returns><c>true</c> if call was successful, <c>false</c> otherwise</returns> 
        private async Task<bool> CallSensorcoreApiAsync( Func<Task> action )
        {
            Exception failure = null;
            try
            {
                await action();
            }
            catch( Exception e )
            {
                failure = e;
            }
            if( failure != null )
            {
                MessageDialog dialog;
                switch( SenseHelper.GetSenseError( failure.HResult ) )
                {
                    case SenseError.LocationDisabled:
                        dialog = new MessageDialog( "Location has been disabled. Do you want to open Location settings now?", "Information" );
                        dialog.Commands.Add( new UICommand( "Yes", async cmd => await SenseHelper.LaunchLocationSettingsAsync() ) );
                        dialog.Commands.Add( new UICommand( "No" ) );
                        await dialog.ShowAsync();
                        new System.Threading.ManualResetEvent( false ).WaitOne( 500 );
                        return false;
                    case SenseError.SenseDisabled:
                        dialog = new MessageDialog( "Motion data has been disabled. Do you want to open Motion data settings now?", "Information" );
                        dialog.Commands.Add( new UICommand( "Yes", async cmd => await SenseHelper.LaunchSenseSettingsAsync() ) );
                        dialog.Commands.Add( new UICommand( "No" ) );
                        await dialog.ShowAsync();
                        new System.Threading.ManualResetEvent( false ).WaitOne( 500 );
                        return false;
                    case SenseError.SensorNotAvailable:
                        dialog = new MessageDialog( "The sensor is not supported on this device", "Information" );
                        await dialog.ShowAsync();
                        new System.Threading.ManualResetEvent( false ).WaitOne( 500 );
                        return false;
                    default:
                        dialog = new MessageDialog( "Failure: " + SenseHelper.GetSenseError( failure.HResult ), "" );
                        await dialog.ShowAsync();
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Displays current step counter reading
        /// </summary>
        /// <returns>Asynchronous task</returns>
        private async Task ShowCurrentReading()
        {
            await CallSensorcoreApiAsync( async () =>
            {
                // Get current reading from the sensor and display it in UI
                var reading = await _stepCounter.GetCurrentReadingAsync();
                SensorcoreList.Items.Add( "Current step counter reading" );
                if( reading != null )
                {
                    SensorcoreList.Items.Clear();
                    SensorcoreList.Items.Add(reading.Timestamp.ToString());
                    SensorcoreList.Items.Add("Walk steps = " + reading.WalkingStepCount);
                    SensorcoreList.Items.Add("Walk time = " + reading.WalkTime.ToString());
                    SensorcoreList.Items.Add("Run steps = " + reading.RunningStepCount );
                    SensorcoreList.Items.Add("Run time = " + reading.RunTime.ToString());
                }
                else
                {
                    SensorcoreList.Items.Add( "Data not available" );
                }
            } );
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            CheckMotionDataSettings();
        }

        /// <summary>
        /// Play preinstalled recording button click handler
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="args">Event arguments</param>
        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            SenseRecording recording = null;
            WebErrorStatus exceptionDetail = new WebErrorStatus();
            try
            {
                recording = await SenseRecording.LoadFromUriAsync(new Uri("https://github.com/Microsoft/steps/raw/master/Steps/Simulations/short%20walk.txt"));
            }
            catch (Exception ex)
            {
                exceptionDetail = WebError.GetStatus(ex.GetBaseException().HResult);
            }
            if (exceptionDetail == WebErrorStatus.HostNameNotResolved)
            {
                MessageDialog dialog = new MessageDialog("Check your network connection. Host name could not be resolved.", "Information");
                await dialog.ShowAsync();
            }
            if (recording != null)
            {
                _stepCounter = await StepCounterSimulator.GetDefaultAsync(recording);
                MessageDialog dialog = new MessageDialog(
                "Recorded sensor type: " + recording.Type.ToString() +
                "\r\nDescription: " + recording.Description +
                "\r\nRecording date: " + recording.StartTime.ToString() +
                "\r\nDuration: " + recording.Duration.ToString(),
                "Recording info"
                );
                await dialog.ShowAsync();
            }
        }
    }
}
