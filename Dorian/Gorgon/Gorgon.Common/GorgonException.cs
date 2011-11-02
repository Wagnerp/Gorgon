#region MIT.
// 
// Gorgon.
// Copyright (C) 2011 Michael Winsor
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// Created: Tuesday, June 14, 2011 8:56:44 PM
// 
#endregion

using System;
using System.Runtime.Serialization;
using GorgonLibrary.Diagnostics;

namespace GorgonLibrary
{
	/// <summary>
	/// Delegate to define an exception handler.
	/// </summary>
	public delegate void GorgonExceptionHandler();

	/// <summary>
	/// Primary exception used for Gorgon.
	/// </summary>
	public class GorgonException
		: Exception
	{
		#region Properties.
		/// <summary>
		/// Property to set or return the log system to use when dumping exceptions to the log.
		/// </summary>
		public static GorgonLogFile Log
		{
			get;
			set;
		}

		/// <summary>
		/// Property to return the exception result code.
		/// </summary>
		public GorgonResult ResultCode
		{
			get;
			private set;
		}
		#endregion

		#region Methods.
		/// <summary>
		/// Function to format a stack trace to be more presentable.
		/// </summary>
		/// <param name="stack">Stack trace to format.</param>
		/// <param name="indicator">Inner exception indicator.</param>
		/// <param name="logLevel">Logging level to use.</param>
		private static void FormatStackTrace(string stack, string indicator, GorgonLoggingLevel logLevel)
		{
			string[] lines = null;		// List of lines.

			if (string.IsNullOrEmpty(stack))
				return;

			stack = stack.Replace('\t', ' ');
			lines = stack.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

			Log.Print("{0}Stack trace:", logLevel, indicator);
			for (int i = lines.Length - 1; i >= 0; i--)
			{
				int inIndex = lines[i].LastIndexOf(") in ");
				int pathIndex = lines[i].LastIndexOf(@"\");

				if ((inIndex > -1) && (pathIndex > -1))
					lines[i] = lines[i].Substring(0, inIndex + 5) + lines[i].Substring(pathIndex + 1);

				Log.Print("{1}{0}", logLevel, lines[i], indicator);
			}

			Log.Print("{0}<<<END>>>", logLevel, indicator);
		}

		/// <summary>
		/// Function to format the exception message for the log output.
		/// </summary>
		/// <param name="message">Message to format.</param>
		/// <param name="indicator">Inner exception indicator.</param>
		/// <param name="logLevel">Logging level to use.</param>
		private static void FormatMessage(string message, string indicator, GorgonLoggingLevel logLevel)
		{
			string[] lines = null;		// List of lines.

			if (string.IsNullOrEmpty(message))
				return;

			message = message.Replace('\t', ' ');
			lines = message.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

			for (int i = 0; i < lines.Length; i++)
			{
				if (i == 0)
					Log.Print("{1}Exception: {0}", logLevel, lines[i], indicator);
				else
					Log.Print("{1}           {0}", logLevel, lines[i], indicator);
			}
		}

		/// <summary>
		/// Function to send the exception to the log file.
		/// </summary>
		private static void LogException(Exception ex)
		{
			Exception inner = null;				// Inner exception.
			string indicator = string.Empty;	// Inner exception indicator.
			string branch = string.Empty;		// Branching character.

			if (Log == null)
				return;

			if (ex == null)
				return;

			Log.Print("", GorgonLoggingLevel.All);
			Log.Print("================================================", GorgonLoggingLevel.All);
			Log.Print("\tEXCEPTION!!", GorgonLoggingLevel.All);
			Log.Print("================================================", GorgonLoggingLevel.All);

			inner = ex;
			while (inner != null)
			{
				GorgonException gorgonException = inner as GorgonException;

				FormatMessage(inner.Message, indicator, (inner == ex) ? GorgonLoggingLevel.All : GorgonLoggingLevel.Verbose);
				Log.Print("{1}Type: {0}", (inner == ex) ? GorgonLoggingLevel.All : GorgonLoggingLevel.Verbose, inner.GetType().FullName, indicator);
				if (inner.Source != null)
					Log.Print("{1}Source: {0}", (inner == ex) ? GorgonLoggingLevel.All : GorgonLoggingLevel.Verbose, inner.Source, indicator);
				if (inner.TargetSite != null)
					Log.Print("{1}Target site: {0}", (inner == ex) ? GorgonLoggingLevel.All : GorgonLoggingLevel.Verbose, inner.TargetSite.DeclaringType.FullName + "." + inner.TargetSite.Name, indicator);
				if (gorgonException != null)
					Log.Print("{2}Result Code: {0} (0x{1:X})", (inner == ex) ? GorgonLoggingLevel.All : GorgonLoggingLevel.Verbose, gorgonException.ResultCode.Name, gorgonException.ResultCode.Code, indicator);

				System.Collections.IDictionary extraInfo = inner.Data;

				// Print custom information.
				if ((extraInfo != null) && (extraInfo.Count > 0))
				{
					Log.Print("{0}", GorgonLoggingLevel.Verbose, indicator);
					Log.Print("{0}Custom Information:", GorgonLoggingLevel.Verbose, indicator);
					Log.Print("{0}------------------------------------------------------------", GorgonLoggingLevel.Verbose, indicator);
					foreach (System.Collections.DictionaryEntry item in extraInfo)
					{
						if ((item.Value != null) && (item.Key != null))
							Log.Print("{0}{1}:  {2}", GorgonLoggingLevel.Verbose, indicator, item.Key, item.Value);
					}
					Log.Print("{0}------------------------------------------------------------", GorgonLoggingLevel.Verbose, indicator);
					Log.Print("{0}", GorgonLoggingLevel.Verbose, indicator);
				}
				
				FormatStackTrace(inner.StackTrace, indicator, (inner == ex) ? GorgonLoggingLevel.All : GorgonLoggingLevel.Verbose);
				
				if (inner.InnerException != null)
				{
					if (indicator != string.Empty)
					{
						Log.Print("{0}================================================================================================", GorgonLoggingLevel.Verbose, branch + "|->   ");
						branch += "  ";
						indicator = branch + "|   ";
					}
					else
					{
						Log.Print("{0}================================================================================================", GorgonLoggingLevel.Verbose, branch + "|-> ");
						indicator = "|   ";
					}
										
					Log.Print("{0}  Inner exception from \"{1}\"", GorgonLoggingLevel.Verbose, indicator, inner.Message);
					Log.Print("{0}================================================================================================", GorgonLoggingLevel.Verbose, indicator);
				}

				inner = inner.InnerException;
			}
			Log.Print("", GorgonLoggingLevel.All);			
		}

		/// <summary>
		/// When overridden in a derived class, sets the <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with information about the exception.
		/// </summary>
		/// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
		/// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is a null reference (Nothing in Visual Basic). </exception>
		/// <PermissionSet>
		/// 	<IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Read="*AllFiles*" PathDiscovery="*AllFiles*"/>
		/// 	<IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="SerializationFormatter"/>
		/// </PermissionSet>
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);

			info.AddValue("ResultCode", ResultCode, typeof(GorgonResult));
		}

		/// <summary>
		/// Function to catch and log any stray exception.
		/// </summary>
		/// <param name="ex">Exception to catch.</param>
		/// <returns>The exception that was caught.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="ex"/> parameter is NULL (or Nothing in VB.NET).</exception>
		public static Exception Catch(Exception ex)
		{
			if (ex == null)
				throw new ArgumentNullException("ex");

			LogException(ex);

			return ex;
		}

		/// <summary>
		/// Functon to catch and handle an exception.
		/// </summary>
		/// <param name="ex">Exception to pass to the handler.</param>
		/// <param name="handler">Handler to handle the exception.</param>
		/// <returns>The exception that was caught.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="ex"/> or <paramref name="handler"/> parameter is NULL (or Nothing in VB.NET).</exception>
		public static Exception Catch(Exception ex, GorgonExceptionHandler handler)
		{
			if (ex == null)
				throw new ArgumentNullException("ex");

			if (handler == null)
				throw new ArgumentNullException("handler");

			LogException(ex);
			handler();

			return ex;
		}

		/// <summary>
		/// Function to repackage an arbitrary exception as an Gorgon exception.
		/// </summary>
		/// <param name="result">Result code to use.</param>
		/// <param name="message">Message to append to the result.</param>
		/// <param name="ex">Exception to capture and rethrow.</param>
		/// <returns>A new Gorgon exception to throw.</returns>
		/// <remarks>The original exception will be the inner exception of the new <see cref="T:GorgonLibrary.GorgonException"/>.</remarks>
		/// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="ex"/> parameter is NULL (or Nothing in VB.NET).</exception>
		public static GorgonException Repackage(GorgonResult result, string message, Exception ex)
		{
			if (ex == null)
				throw new ArgumentNullException("ex");

			return new GorgonException(result, message, ex);
		}

		/// <summary>
		/// Function to repackage an arbitrary exception as an Gorgon exception.
		/// </summary>
		/// <param name="result">Result code to use.</param>
		/// <param name="ex">Exception to capture and rethrow.</param>
		/// <returns>A new Gorgon exception to throw.</returns>
		/// <remarks>The original exception will be the inner exception of the new <see cref="T:GorgonLibrary.GorgonException"/>.</remarks>
		/// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="ex"/> parameter is NULL (or Nothing in VB.NET).</exception>
		public static GorgonException Repackage(GorgonResult result, Exception ex)
		{
			if (ex == null)
				throw new ArgumentNullException("ex");

			return new GorgonException(result, ex);
		}

		/// <summary>
		/// Function to repackage an arbitrary exception as an Gorgon exception.
		/// </summary>
		/// <param name="message">New message to pass to the new exception.</param>
		/// <param name="ex">Exception to capture and rethrow.</param>
		/// <returns>A new Gorgon exception to throw.</returns>
		/// <remarks>The original exception will be the inner exception of the new <see cref="T:GorgonLibrary.GorgonException"/>.</remarks>
		/// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="ex"/> parameter is NULL (or Nothing in VB.NET).</exception>
		public static GorgonException Repackage(string message, Exception ex)
		{
			if (ex == null)
				throw new ArgumentNullException("ex");

			return new GorgonException(message, ex);
		}
		#endregion

		#region Constructor/Destructor.
		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="errorMessage">Error message to display.</param>
		/// <param name="innerException">Inner exception to pass through.</param>
		public GorgonException(string errorMessage, Exception innerException)
			: base(errorMessage, innerException)
		{
			ResultCode = new GorgonResult("GorgonException", this.HResult, errorMessage);
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="errorMessage">Error message to display.</param>
		public GorgonException(string errorMessage)
			: base(errorMessage)
		{
			ResultCode = new GorgonResult("GorgonException", this.HResult, errorMessage);
		}

		/// <summary>
		/// Serialized constructor.
		/// </summary>
		/// <param name="info">Serialization info.</param>
		/// <param name="context">Serialization context.</param>
		protected GorgonException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			if (info.FullTypeName == typeof(GorgonResult).FullName)
				ResultCode = (GorgonResult)info.GetValue("ResultCode", typeof(GorgonResult));
			else
				ResultCode = new GorgonResult("Exception", info.GetInt32("HResult"), info.GetString("Message"));
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonException"/> class.
		/// </summary>
		/// <param name="result">The result.</param>
		/// <param name="message">Message data to append to the error.</param>
		/// <param name="inner">The inner exception.</param>
		public GorgonException(GorgonResult result, string message, Exception inner)
			: base(result.Description + (!string.IsNullOrEmpty(message) ? "\n" + message : string.Empty), inner)
		{
			ResultCode = result;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonException"/> class.
		/// </summary>
		/// <param name="result">The result.</param>
		/// <param name="message">Message data to append to the error.</param>
		public GorgonException(GorgonResult result, string message)
			: base(result.Description + (!string.IsNullOrEmpty(message) ? "\n" + message : string.Empty))
		{
			ResultCode = result;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonException"/> class.
		/// </summary>
		/// <param name="result">The result.</param>
		/// <param name="inner">The inner exception.</param>
		public GorgonException(GorgonResult result, Exception inner)
			: this(result, null, inner)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GorgonException"/> class.
		/// </summary>
		/// <param name="result">The result.</param>
		public GorgonException(GorgonResult result)
			: this(result, null, null)
		{
		}

		/// <summary>
		/// Default constructor.
		/// </summary>
		public GorgonException()
		{
			ResultCode = new GorgonResult("GorgonException", int.MinValue, string.Empty);
		}
		#endregion
	}
}
