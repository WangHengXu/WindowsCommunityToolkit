// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;

namespace Microsoft.Toolkit.Win32.UI.Interop
{
    /// <summary>
    /// WindowsXamlHost control hosts UWP XAML content inside the Windows Presentation Foundation
    /// </summary>
    public partial class WindowsXamlHost : HwndHost
    {
        #region DependencyProperties

        /// <summary>
        /// UWP XAML Application instance and root UWP XamlMetadataProvider.  Custom implementation required to 
        /// probe at runtime for custom UWP XAML type information.  This must be created before 
        /// creating any DesktopWindowXamlSource instances if custom UWP XAML types are required.
        /// </summary>
        [ThreadStatic]
        private global::Windows.UI.Xaml.Application application;

        /// <summary>
        /// XAML Content by type name : MyNamespace.MyClass.MyType
        /// ex: XamlClassLibrary.MyUserControl
        /// </summary>
        public static DependencyProperty TypeNameProperty = DependencyProperty.Register(nameof(TypeName), typeof(string), typeof(WindowsXamlHost));

        /// <summary>
        /// Root UWP XAML element displayed in the WindowsXamlHost control.  This UWP XAML element is 
        /// the root element of the wrapped DesktopWindowXamlSource instance.
        /// </summary>
        public static DependencyProperty XamlRootProperty = DependencyProperty.Register(nameof(XamlRoot), typeof(global::Windows.UI.Xaml.UIElement), typeof(WindowsXamlHost));

        
        #endregion

        #region Fields

        /// <summary>
        /// UWP XAML DesktopWindowXamlSource instance that hosts XAML content in a win32 application
        /// </summary>
        public global::Windows.UI.Xaml.Hosting.DesktopWindowXamlSource desktopWindowXamlSource;

        /// <summary>
        /// Has this wrapper control instance been disposed?
        /// </summary>
        private bool IsDisposed { get; set; }

        /// <summary>
        /// A reference count on the UWP XAML framework is tied to WindowsXamlManager's 
        /// lifetime.  UWP XAML is spun up on the first WindowsXamlManager creation and 
        /// deinitialized when the last instance of WindowsXamlManager is destroyed.
        /// </summary>
        global::Windows.UI.Xaml.Hosting.WindowsXamlManager windowsXamlManager;

        #endregion

        #region Constructors and Initialization

        public WindowsXamlHost(string typeName)
            : this()
        {
            TypeName = typeName;
            
            // Create and set initial root UWP XAML content
            if (this.TypeName != null)
            {
                this.XamlRoot = this.CreateXamlContentByType(this.TypeName);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsXamlHost"/> class.
        /// Initializes a new instance of the WindowsXamlHost class: default constructor is required for use in WPF markup.
        /// (When the default constructor is called, object properties have not been set. Put WPF logic in OnInitialized.)
        /// </summary>
        public WindowsXamlHost()
        {
            // Create a custom UWP XAML Application object that implements reflection-based XAML metdata probing.
            // Instantiation of the application object must occur before creating the DesktopWindowXamlSource instance. 
            // DesktopWindowXamlSource will create a generic Application object unable to load custom UWP XAML metadata.
            if (this.application == null)
            {
                try
                {
                    // global::Windows.UI.Xaml.Application.Current may throw if DXamlCore has not been initialized.
                    // Treat the exception as an uninitialized global::Windows.UI.Xaml.Application condition.
                    this.application = global::Windows.UI.Xaml.Application.Current as XamlApplication;
                }
                catch
                {
                    this.application = new XamlApplication();
                }
            }

            // Create an instance of the WindowsXamlManager. This initializes and holds a 
            // reference on the UWP XAML DXamlCore and must be explicitly created before 
            // any UWP XAML types are programmatically created.  If WindowsXamlManager has 
            // not been created before creating DesktopWindowXamlSource, DesktopWindowXaml source
            // will create an instance of WindowsXamlManager internally.  (Creation is explicit
            // here to illustrate how to initialize UWP XAML before initializing the DesktopWindowXamlSource.) 
            windowsXamlManager = global::Windows.UI.Xaml.Hosting.WindowsXamlManager.InitializeForCurrentThread();

            // Create DesktopWindowXamlSource, host for UWP XAML content
            this.desktopWindowXamlSource = new global::Windows.UI.Xaml.Hosting.DesktopWindowXamlSource();

            // Hook OnTakeFocus event for Focus processing
            this.desktopWindowXamlSource.TakeFocusRequested += this.OnTakeFocusRequested;
        }

        /// <summary>
        /// Binds this wrapper object's exposed WPF DependencyProperty with the wrapped UWP object's DependencyProperty
        /// for what becomes effectively a two-way binding.
        /// </summary>
        /// <param name="propertyName">the registered name of the dependency property</param>
        /// <param name="wpfProperty">the DependencyProperty of the wrapper</param>
        /// <param name="uwpProperty">the related DependencyProperty of the UWP control</param>
        /// <param name="converter">a converter, if one's needed</param>
        public void Bind(string propertyName, DependencyProperty wpfProperty, global::Windows.UI.Xaml.DependencyProperty uwpProperty, object converter = null, BindingDirection direction = BindingDirection.TwoWay)
        {
            if (direction == BindingDirection.TwoWay)
            {
                var binder = new global::Windows.UI.Xaml.Data.Binding()
                {
                    Source = this,
                    Path = new global::Windows.UI.Xaml.PropertyPath(propertyName),
                    Converter = (global::Windows.UI.Xaml.Data.IValueConverter)converter
                };
                global::Windows.UI.Xaml.Data.BindingOperations.SetBinding(XamlRoot, uwpProperty, binder);
            }

            var rebinder = new Binding()
            {
                Source = XamlRoot,
                Path = new PropertyPath(propertyName),
                Converter = (IValueConverter)converter
            };
            BindingOperations.SetBinding(this, wpfProperty, rebinder);
        }

        /// <summary>
        /// Creates initial UWP XAML content if TypeName has been set
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // Create and set initial root UWP XAML content
            if (this.TypeName != null && XamlRoot == null)
            {
                this.XamlRoot = this.CreateXamlContentByType(this.TypeName);
            }
        }

        #endregion

        #region Events
        /// <summary>
        ///     Fired when WindowsXamlHost root UWP XAML content has been updated
        /// </summary>
        public event EventHandler XamlContentUpdated;
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets XAML Content by type name : MyNamespace.MyClass.MyType
        /// ex: XamlClassLibrary.MyUserControl
        /// (Content creation is deferred until after the parent hwnd has been created.)
        /// </summary>
        [Browsable(true)]
        [Category("XAML")]
        public virtual string TypeName
        {
            get
            {
                return (string)GetValue(TypeNameProperty);
            }

            set
            {
                SetValue(TypeNameProperty, value);
            }
        }

        /// <summary>
        /// Gets or sets the root UWP XAML element displayed in the WPF control instance.  This UWP XAML element is 
        /// the root element of the wrapped DesktopWindowXamlSource.
        /// </summary>
        [Browsable(true)]
        public virtual global::Windows.UI.Xaml.UIElement XamlRoot
        {
            get
            {
                return (global::Windows.UI.Xaml.UIElement)GetValue(XamlRootProperty);
            }

            set
            {
                // TODO: Fix and cleanup this entire method. Remove unnecessary layout events.
                traceSource.TraceEvent(TraceEventType.Verbose, 0, "Setting Content...");

                if (value == (global::Windows.UI.Xaml.UIElement)GetValue(XamlRootProperty))
                {
                    traceSource.TraceEvent(TraceEventType.Verbose, 0, "Content: Content unchanged.");

                    return;
                }

                global::Windows.UI.Xaml.FrameworkElement currentRoot = (global::Windows.UI.Xaml.FrameworkElement)GetValue(XamlRootProperty);
                if (currentRoot != null)
                {
                    // COM object separated from its current RCW cannot be used. Don't try to set
                    // Content to NULL after DesktopWindowXamlSource has been destroyed.  
                    currentRoot.LayoutUpdated -= this.XamlContentLayoutUpdated;

                    currentRoot.SizeChanged -= this.XamlContentSizeChanged;
                }

                // TODO: Add special case for NULL Content.  This should resize the HwndIslandSite to 0, 0. 
                this.SetValue(WindowsXamlHost.XamlRootProperty, value);
                value?.SetWrapper(this);

                if (this.desktopWindowXamlSource != null)
                {
                    this.desktopWindowXamlSource.Content = value;
                }

                global::Windows.UI.Xaml.FrameworkElement frameworkElement = value as global::Windows.UI.Xaml.FrameworkElement;
                if (frameworkElement != null)
                {
                    // If XAML content has changed, check XAML size 
                    // to determine if WindowsXamlHost needs to re-run layout.
                    frameworkElement.LayoutUpdated += this.XamlContentLayoutUpdated;
                    frameworkElement.SizeChanged += this.XamlContentSizeChanged;

                    // WindowsXamlHost DataContext should flow through to UWP XAML content
                    frameworkElement.DataContext = this.DataContext;
                }

                // Fire updated event
                if (this.XamlContentUpdated != null)
                {
                    this.XamlContentUpdated(this, new System.EventArgs());
                }
            }
        }

        public static global::Windows.UI.Xaml.DependencyProperty WrapperProperty { get; } =
            global::Windows.UI.Xaml.DependencyProperty.RegisterAttached("Wrapper", typeof(System.Windows.UIElement), typeof(WindowsXamlHost), new global::Windows.UI.Xaml.PropertyMetadata(null));


        #endregion

        #region Methods

        /// <summary>
        /// Creates global::Windows.UI.Xaml.Application object, wrapped DesktopWindowXamlSource instance; creates and
        /// sets root UWP XAML element on DesktopWindowXamlSource.
        /// </summary>
        /// <param name="hwndParent">Parent window handle</param>
        /// <returns>Handle to XAML window</returns>
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            // 'EnableMouseInPointer' is called by the WindowsXamlManager during initialization. No need
            // to call it directly here. 

            // Create DesktopWindowXamlSource instance
            IDesktopWindowXamlSourceNative desktopWindowXamlSourceNative = this.desktopWindowXamlSource.GetInterop();

            // Associate the window where UWP XAML will display content
            desktopWindowXamlSourceNative.AttachToWindow(hwndParent.Handle);

            IntPtr windowHandle = desktopWindowXamlSourceNative.WindowHandle;

            // Overridden function must return window handle of new target window (DesktopWindowXamlSource's Window)
            return new HandleRef(this, windowHandle);
        }

        /// <summary>
        /// WPF framework request to destroy control window.  Cleans up the HwndIslandSite created by DesktopWindowXamlSource
        /// </summary>
        /// <param name="hwnd">Handle of window to be destroyed</param>
        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            this.Dispose(true);
        }

        /// <summary>
        /// WindowsXamlHost Dispose
        /// </summary>
        /// <param name="disposing">Is disposing?</param>
        protected override void Dispose(bool disposing)
        { 
            if (disposing && !this.IsDisposed)
            {
                this.IsDisposed = true;
                this.desktopWindowXamlSource.TakeFocusRequested -= this.OnTakeFocusRequested;
                this.XamlRoot = null;
                this.desktopWindowXamlSource.Dispose();
                this.desktopWindowXamlSource = null;
            }
        }

        #endregion
    }

    public static class UwpUIElementExtensions
    {
        public static WindowsXamlHost GetWrapper(this global::Windows.UI.Xaml.UIElement element)
        {
            return (WindowsXamlHost)element.GetValue(WindowsXamlHost.WrapperProperty);
        }

        public static void SetWrapper(this global::Windows.UI.Xaml.UIElement element, WindowsXamlHost wrapper)
        {
            element.SetValue(WindowsXamlHost.WrapperProperty, wrapper);
        }
    }
}
