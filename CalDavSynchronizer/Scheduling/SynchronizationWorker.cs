﻿// This file is Part of CalDavSynchronizer (http://outlookcaldavsynchronizer.sourceforge.net/)
// Copyright (c) 2015 Gerhard Zehetbauer 
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using CalDavSynchronizer.Contracts;
using CalDavSynchronizer.DataAccess;
using CalDavSynchronizer.Diagnostics;
using CalDavSynchronizer.Generic.EntityRelationManagement;
using CalDavSynchronizer.Generic.ProgressReport;
using CalDavSynchronizer.Generic.Synchronization;
using CalDavSynchronizer.Generic.Synchronization.StateFactories;
using CalDavSynchronizer.Generic.Synchronization.States;
using CalDavSynchronizer.Implementation;
using CalDavSynchronizer.Implementation.ComWrappers;
using CalDavSynchronizer.Utilities;
using DDay.iCal;
using log4net;
using Microsoft.Office.Interop.Outlook;
using Exception = System.Exception;

namespace CalDavSynchronizer.Scheduling
{
  internal class SynchronizationWorker
  {
    private static readonly ILog s_logger = LogManager.GetLogger (MethodInfo.GetCurrentMethod().DeclaringType);

    private DateTime _lastRun;
    private TimeSpan _interval;
    private ISynchronizer _synchronizer;
    private string _profileName;
    private readonly string _outlookEmailAddress;
    private bool _inactive;
    private readonly ITotalProgressFactory _totalProgressFactory;
    private readonly string _applicationDataDirectory;

    public SynchronizationWorker (string outlookEmailAddress, string applicationDataDirectory)
    {
      _outlookEmailAddress = outlookEmailAddress;
      _applicationDataDirectory = applicationDataDirectory;
      // Set to min, to ensure that it runs on the first run after startup
      _lastRun = DateTime.MinValue;
      _totalProgressFactory = new TotalProgressFactory(
        new Ui.ProgressFormFactory(),
        int.Parse(ConfigurationManager.AppSettings["loadOperationThresholdForProgressDisplay"]));
    }

    public void UpdateOptions (NameSpace outlookSession, Options options)
    {
      _profileName = options.Name;

      var storageDataDirectory = Path.Combine (
         _applicationDataDirectory,
         options.Id.ToString ()
     );

      var storageDataAccess = new EntityRelationDataAccess<string, DateTime, OutlookEventRelationData , Uri, string> (storageDataDirectory);

      var synchronizationContext = new OutlookCalDavEventContext (
        outlookSession, 
        storageDataAccess, 
        options, 
        _outlookEmailAddress, 
        TimeSpan.Parse(ConfigurationManager.AppSettings["calDavConnectTimeout"]), 
        TimeSpan.Parse(ConfigurationManager.AppSettings["calDavReadWriteTimeout"])
      );

      var syncStateFactory = new EntitySyncStateFactory<string, DateTime, AppointmentItemWrapper, Uri, string, IICalendar> (
        synchronizationContext.EntityMapper,
        synchronizationContext.AtypeRepository,
        synchronizationContext.BtypeRepository,
        synchronizationContext.EntityRelationDataFactory
      );



      _synchronizer = new Synchronizer<string, DateTime, AppointmentItemWrapper, Uri, string, IICalendar> (
          synchronizationContext,
          InitialSyncStateCreationStrategyFactory.Create(
            syncStateFactory,
            syncStateFactory.Environment,
            options.SynchronizationMode,
            options.ConflictResolution),
            _totalProgressFactory
          );

    
      
      _interval = TimeSpan.FromMinutes (options.SynchronizationIntervalInMinutes);
      _inactive = options.Inactive;
    }

    public void RunIfRequiredAndReschedule ()
    {
      if (!_inactive && _interval > TimeSpan.Zero && DateTime.UtcNow > _lastRun + _interval)
      {
        RunNoThrowAndReschedule();
      }
    }

    public void RunNoThrowAndReschedule ()
    {
      if (_inactive)
        return;

      try
      {
        using (AutomaticStopwatch.StartInfo (s_logger, string.Format ("Running synchronization profile '{0}'", _profileName)))
        {
          _synchronizer.Synchronize();
        }
      }
      catch (Exception x)
      {
        ExceptionHandler.Instance.HandleException (x, s_logger);
      }
      finally
      {
        _lastRun = DateTime.UtcNow;
      }
    }
  }
}