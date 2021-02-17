/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace SuperSMBServer
{
    public class ShareSettings
    {
        public string ShareName { get; set; }
        public string SharePath { get; set; }
        public List<string> ReadAccess { get; set; }
        public List<string> WriteAccess { get; set; }

        public ShareSettings(string shareName, string sharePath, List<string> readAccess, List<string> writeAccess)
        {
            ShareName = shareName;
            SharePath = sharePath;
            ReadAccess = readAccess;
            WriteAccess = writeAccess;
        }
    }
}
