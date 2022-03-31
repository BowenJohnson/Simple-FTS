// Bowen Johnson 

using System;
using System.Collections.Generic;
using System.Threading;

namespace SDServer
{
    class SessionException : Exception
    {
        public SessionException(string msg) : base(msg)
        {
        }
    }

    class SessionTable
    {
        // thread safe collection
        // represents the SDServer's session table, where we track session data per client
        // client sessions are identified by an unsigned long session ID
        // session IDs are never reused
        // when the session table is first created, it is empty, with no client session data
        // client session data is made up of arbitrary key/value pairs, where each are text

        private class Session
        {
            public ulong sessionId;
            public Dictionary<string, string> values;
            public DateTime lastUsed;
            // Note: any other info about the session we want to remember can go here

            public Session(ulong sessionId)
            {
                this.sessionId = sessionId;
                values = new Dictionary<string, string>();
                lastUsed = DateTime.Now;
            }
        }

        private Dictionary<ulong, Session> sessions;    // sessionId --> Session instance
        private ulong nextSessionId;                    // next value to use for the next new session
        private Mutex mutex;                            // synchronize access to sessions
        private int timeout;                            // number of seconds before timeout

        public SessionTable(int timeout)
        {
            sessions = new Dictionary<ulong, Session>();
            nextSessionId = 1;
            mutex = new Mutex(false);
            this.timeout = timeout;
        }

        private ulong NextSessionId()
        {
            // watch out for multiple threads trying to get the next sessionId!!!
            ulong sessionId = 0;
            mutex.WaitOne();
            sessionId = nextSessionId++;
            mutex.ReleaseMutex();

            return sessionId;
        }

        public ulong OpenSession()
        {
            // allocate and return a new session to the caller
            // find a free sessionId
            ulong sessionId = NextSessionId();

            // allocate a new session instance
            Session session = new Session(sessionId);

            // save the session for later
            mutex.WaitOne();
            sessions.Add(sessionId, session);
            mutex.ReleaseMutex();

            return sessionId;
        }

        public bool ResumeSession(ulong sessionID)
        {
            // returns true only if sessionID is a valid and open sesssion, false otherwise

            // valid means non-zero
            if (sessionID == 0)
                return false;

            // open means it's in the dictionary
            bool isOpen = false;
            mutex.WaitOne();
            if (sessions.ContainsKey(sessionID))
            {
                // check if expired session
                if ((DateTime.Now - sessions[sessionID].lastUsed).Seconds < timeout)
                {
                    // not expired
                    isOpen = true;
                    sessions[sessionID].lastUsed = DateTime.Now;
                }
                else
                {
                    // close the expired session
                    sessions.Remove(sessionID);
                }
            }
            mutex.ReleaseMutex();

            return isOpen;
        }

        public void CloseSession(ulong sessionID)
        {
            // closes the session, will no longer be open and cannot be reused
            // throws a session exception if the session is not open
            mutex.WaitOne();
            if (sessions.ContainsKey(sessionID))
            {
                // it's open so shut it down
                sessions.Remove(sessionID);
                mutex.ReleaseMutex();
            }
            else
            {
                // not open, this means things have gone wrong
                mutex.ReleaseMutex();
                throw new SessionException("Cannot close session, it's not currently open!");
            }
        }

        public string GetSessionValue(ulong sessionID, string key)
        {
            // retrieves a session value, given session ID and key
            // throws a session exception if the session is not open or if the value does not exist by that key
            mutex.WaitOne();
            if (!sessions.ContainsKey(sessionID))
            {
                mutex.ReleaseMutex();
                throw new SessionException("Cannot get value, session not open!");
            }
            if (!sessions[sessionID].values.ContainsKey(key))
            {
                mutex.ReleaseMutex();
                throw new SessionException("Cannot get value, value does not exist!");
            }
            string value = sessions[sessionID].values[key];
            sessions[sessionID].lastUsed = DateTime.Now;
            mutex.ReleaseMutex();
            return value;
        }

        public void PutSessionValue(ulong sessionID, string key, string value)
        {
            // stores a session value by session ID and key, replaces value if it already exists
            // throws a session exception if the session is not open
            mutex.WaitOne();
            if (!sessions.ContainsKey(sessionID))
            {
                mutex.ReleaseMutex();
                throw new SessionException("Cannot put value, session not open!");
            }

            sessions[sessionID].values[key] = value;
            sessions[sessionID].lastUsed = DateTime.Now;
            mutex.ReleaseMutex();
        }
    }
}
