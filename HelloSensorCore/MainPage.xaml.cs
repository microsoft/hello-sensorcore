using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;
using Lumia.Sense;
using System.Threading.Tasks;

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
        private StepCounter _stepCounter;
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
                    SensorcoreList.Items.Add( reading.Timestamp.ToString() );
                    SensorcoreList.Items.Add( "Walk steps = " + reading.WalkingStepCount );
                    SensorcoreList.Items.Add( "Walk time = " + reading.WalkTime.ToString() );
                    SensorcoreList.Items.Add( "Run steps = " + reading.RunningStepCount );
                    SensorcoreList.Items.Add( "Run time = " + reading.RunTime.ToString() );
                }
                else
                {
                    SensorcoreList.Items.Add( "Data not available" );
                }
            } );
        }
    }
}
