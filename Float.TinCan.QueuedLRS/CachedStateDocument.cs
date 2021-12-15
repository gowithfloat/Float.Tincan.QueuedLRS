using System;
using Newtonsoft.Json.Linq;
using TinCan;
using TinCan.Documents;
using TinCan.Json;

namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// Queued state document.
    /// </summary>
    public class CachedStateDocument
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedStateDocument"/> class.
        /// </summary>
        public CachedStateDocument()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedStateDocument"/> class.
        /// </summary>
        /// <param name="state">State Document to Save.</param>
        /// <param name="status">Status of this document in store e.g clean, dirty.</param>
        public CachedStateDocument(StateDocument state, Status status = Status.Dirty)
        {
            CurrentStatus = status;
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedStateDocument"/> class.
        /// </summary>
        /// <param name="jobj">The JSON object that contains the data for this object.</param>
        public CachedStateDocument(JObject jobj)
        {
            if (jobj == null)
            {
                throw new ArgumentNullException(nameof(jobj));
            }

            if (jobj[nameof(CurrentStatus)] != null)
            {
                CurrentStatus = (Status)jobj.Value<int>(nameof(CurrentStatus));
            }

            if (jobj[nameof(StateDocument)] != null)
            {
                State = DecodeStateDocumentJSON(jobj.Value<JObject>(nameof(StateDocument)));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedStateDocument"/> class.
        /// </summary>
        /// <param name="json">Json Object containing queued state document details.</param>
        public CachedStateDocument(StringOfJSON json) : this(json?.toJObject())
        {
        }

        /// <summary>
        /// The available status values for this state document.
        /// </summary>
        public enum Status
        {
            /// <summary>
            /// This StateDocument has been written to Remote LRS
            /// </summary>
            Clean,

            /// <summary>
            /// This State Document needs to be written to the Remote LRS as the
            /// local copy has been updated since it was last written
            /// </summary>
            Dirty,

            /// <summary>
            /// This State Document has been deleted locally but needs to be deleted remotely
            /// </summary>
            Deleted,
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="CachedStateDocument"/> needs sync or to be deleted.
        /// </summary>
        /// <value>Data from Status enum type.</value>
        public Status CurrentStatus { get; set; }

        /// <summary>
        /// Gets or sets the state Document to be saved.
        /// </summary>
        /// <value>The state.</value>
        public StateDocument State { get; set; }

        /// <summary>
        /// Extracts a JSON object from this state document.
        /// </summary>
        /// <returns>A JSON object.</returns>
        /// <param name="version">The TCAPI version used.</param>
        public JObject ToJObject(TCAPIVersion version)
        {
            var resultObject = new JObject
            {
                { nameof(CurrentStatus), (int)CurrentStatus },
            };

            if (State != null)
            {
                resultObject.Add(nameof(StateDocument), EncodeStateDocumentJSON(version));
            }

            return resultObject;
        }

        /// <summary>
        /// Encodes the state document to json file.
        /// </summary>
        /// <returns>The state document json.</returns>
        /// <param name="version">TCAPI Version Used.</param>
        public JObject EncodeStateDocumentJSON(TCAPIVersion version)
        {
            var resultObject = new JObject();

            if (State.id != null)
            {
                resultObject.Add("id", State.id);
            }

            if (State.etag != null)
            {
                resultObject.Add("etag", State.etag);
            }

            if (State.contentType != null)
            {
                resultObject.Add("contentType", State.contentType);
            }

            if (State.content != null)
            {
                resultObject.Add("content", Convert.ToBase64String(State.content));
            }

            if (State.timestamp != default)
            {
                resultObject.Add("timestamp", State.timestamp);
            }

            if (State.agent != null)
            {
                resultObject.Add("agent", State.agent.ToJObject(version));
            }

            if (State.activity != null)
            {
                resultObject.Add("activity", State.activity.ToJObject(version));
            }

            if (State.registration != null)
            {
                resultObject.Add("registration", State.registration.ToString());
            }

            return resultObject;
        }

        /// <summary>
        /// The State document object does not contain a constructor to allow inialization from JSON object.
        /// This method takes JSON object and returns an initialized TinCan.StateDocument object.
        /// </summary>
        /// <returns>A state document object intialized by the JSON object.</returns>
        /// <param name="jobj">A JSON object containing data to itialize the state document.</param>
        static StateDocument DecodeStateDocumentJSON(JObject jobj)
        {
            if (jobj == null)
            {
                throw new ArgumentNullException(nameof(jobj));
            }

            var returnState = new StateDocument();

            if (jobj["id"] != null)
            {
                returnState.id = jobj.Value<string>("id");
            }

            if (jobj["etag"] != null)
            {
                returnState.etag = jobj.Value<string>("etag");
            }

            if (jobj["contentType"] != null)
            {
                returnState.contentType = jobj.Value<string>("contentType");
            }

            if (jobj["timestamp"] != null)
            {
                returnState.timestamp = jobj.Value<DateTime>("timestamp");
            }

            if (jobj["content"] != null)
            {
                returnState.content = Convert.FromBase64String(jobj.Value<string>("content"));
            }

            if (jobj["activity"] != null)
            {
                returnState.activity = new Activity(jobj.Value<JObject>("activity"));
            }

            if (jobj["agent"] != null)
            {
                returnState.agent = new Agent(jobj.Value<JObject>("agent"));
            }

            if (jobj["registration"] != null)
            {
                returnState.registration = new Guid(jobj.Value<string>("registration"));
            }

            return returnState;
        }
    }
}
