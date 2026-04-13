using System;

namespace ASCOM.ArduSafeMon
{
    /// <summary>
    /// Abstraction du polling série, permet de mocker dans les tests.
    /// </summary>
    internal interface ISerialPoller : IDisposable
    {
        /// <summary>Dernier état lu sur l'Arduino (thread-safe).</summary>
        bool IsSafe { get; }

        /// <summary>Ouvre le port série et démarre le thread de polling.</summary>
        void Start();

        /// <summary>Arrête le thread de polling et ferme le port série.</summary>
        void Stop();
    }
}
