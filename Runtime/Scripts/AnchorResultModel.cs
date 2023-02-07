// Copyright (c) Solipsist Studios Inc.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solipsist.Models
{
    [Serializable]
    public class AzureResultModel
    {
        public string contentType;
        public string serializerSettings;
        public string statusCode;
    }

    [Serializable]
    public class AnchorResultModel<T> : AzureResultModel
    {
        public List<T> value;
    }
}
