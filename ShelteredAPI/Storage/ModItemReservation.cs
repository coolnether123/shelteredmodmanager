using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Persistence;
using ShelteredAPI.UI.Runtime;
using UnityEngine;
using GameItemDefinition = global::ItemDefinition;

namespace ShelteredAPI.Storage
{
    internal sealed class ModItemReservation
    {
        public string ReservationId;
        public string ItemId;
        public int Quantity;
        public string OwnerToken;
    }
}
