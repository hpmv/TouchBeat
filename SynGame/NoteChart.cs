using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SynGame {
    internal struct NotePoint {
        public int force;
        public float step;
        public float x;

        public NotePoint(float x, int force, int step) {
            this.x = x;
            this.force = force;
            this.step = step;
        }
    }

    internal class NoteTrace {
        public List<NotePoint> points;

        public void parse(Scanner scanner)
        {
            int n = scanner.nextInt();
            float offset = (float) scanner.nextDouble();
            points = new List<NotePoint>();
            NotePoint[] ps = new NotePoint[n];
            for (int i = 0; i < n; i++) {
                ps[i].step = offset + (float) scanner.nextDouble();
            }
            for (int i = 0; i < n; i++)
            {
                ps[i].x = (float)scanner.nextDouble();
            }
            for (int i = 0; i < n; i++)
            {
                ps[i].force = scanner.nextInt();
            }
            points.AddRange(ps);
        }
    }

    internal class NoteChart {
        public float bpm;
        public float sync;
        public List<NoteTrace> traces;

        public void parse(Scanner scanner) {
            bpm = (float) scanner.nextDouble();
            sync = (float) scanner.nextDouble();
            traces = new List<NoteTrace>();
            while(scanner.hasNext()) {
                var trace = new NoteTrace();
                trace.parse(scanner);
                traces.Add(trace);
            }
        }

        public RawNotePoint ToRaw(NotePoint point) {
            return new RawNotePoint(point.x, point.step/bpm + sync, ToRawForce(point.force));
        }

        private float ToRawForce(int force) {
            if (force == 1) {
                return 0.04f;
            }
            if (force == 2) {
                return 0.07f;
            }
            if (force == 3) {
                return 0.1f;
            }
            return 0.02f;
        }

        public RawNoteTrace ToRaw(NoteTrace trace) {
            var t = new RawNoteTrace();
            foreach (NotePoint point in trace.points) {
                t.points.Add(ToRaw(point));
            }
            return t;
        }

        public static RawNotePoint Interpolate(RawNotePoint p1, RawNotePoint p2, double t) {
            return new RawNotePoint(
                (float) (p1.x*(1 - t) + p2.x*t),
                p1.time*(1 - t) + p2.time*t,
                (float) (p1.force*(1 - t) + p2.force*t));
        }

        public static RawNotePoint Interpolate(RawNoteTrace trace, double t) {
            for (int i = 0; i < trace.points.Count - 1; i++) {
                if (trace.points[i + 1].time >= t) {
                    RawNotePoint p1 = trace.points[i];
                    RawNotePoint p2 = trace.points[i + 1];
                    var rawNotePoint = Interpolate(p1, p2, (t-p1.time)/ (p2.time-p1.time));
                    Debug.WriteLine(rawNotePoint.time);
                    return rawNotePoint;
                }
            }
            return trace.points[trace.points.Count - 1];
        }

        public static List<RawNoteTrace> GetIntersectingTraces(List<RawNoteTrace> traces, float time) {
            // slow method...
            var t = new List<RawNoteTrace>();
            foreach (RawNoteTrace rawNoteTrace in traces) {
                if (rawNoteTrace.contains(time)) {
                    t.Add(rawNoteTrace);
                }
            }
            return t;
        }
    }

    internal class RawNotePoint {
        public float force;
        public double time;
        public float x;

        public RawNotePoint(float x, double time, float force) {
            this.x = x;
            this.time = time;
            this.force = force;
        }
    }

    internal class RawNoteTrace {
        public List<RawNotePoint> points = new List<RawNotePoint>();

        public double FirstTime {
            get { return points[0].time; }
        }

        public double LastTime {
            get { return points[points.Count - 1].time; }
        }

        public bool IsSingle {
            get { return points.Count == 1; }
        }

        public bool contains(float time) {
            return points[0].time <= time && points[points.Count - 1].time >= time;
        }

        public RawNoteTrace CutOff(double time) {
            var result = new List<RawNotePoint>();
            result.Add(NoteChart.Interpolate(this, time));
            foreach (RawNotePoint p in points) {
                if (p.time < time) {
                    continue;
                }
                result.Add(p);
            }
            return new RawNoteTrace {points = result};
        }
    }

    internal struct CurrentNoteRendering {
        public List<RawNoteTrace> future;
        public List<RawNoteTrace> inProgress;
        public List<RawNoteTrace> missed;
    }

    internal class GameSession {
        private const float DISTANCE_THRESHOLD = 0.3f;
        private const float FAIL_TIME_THRESHOLD = 0.5f;
        public float bpm;
        public List<RawNoteTrace> completed = new List<RawNoteTrace>();
        public List<RawNoteTrace> future = new List<RawNoteTrace>();
        public List<RawNoteTrace> inProgress = new List<RawNoteTrace>();
        public List<RawNoteTrace> missed = new List<RawNoteTrace>();
        public double score = 0.0;

        public CurrentNoteRendering FeedInstant(float[] fingers, double time, double futureTime, TimeSpan passed) {
            var assignments = new int[fingers.Length];

            var inProgressIntersect = new List<RawNotePoint>();
            foreach (RawNoteTrace trace in inProgress) {
                inProgressIntersect.Add(NoteChart.Interpolate(trace, time));
            }

            for (int i = 0; i < fingers.Length; i++) {
                float finger = fingers[i];
                int bestTrace = -1;
                for (int index = 0; index < inProgressIntersect.Count; index++) {
                    RawNotePoint point = inProgressIntersect[index];
                    if (Math.Abs(point.x - finger) < DISTANCE_THRESHOLD) {
                        bestTrace = index;
                    }
                }
                assignments[i] = bestTrace;
            }
            var inProgressHit = new bool[inProgress.Count];
            foreach (int assignment in assignments) {
                if (assignment != -1)
                    inProgressHit[assignment] = true;
            }
            for (int i = 0; i < inProgress.Count; i++) {
                RawNoteTrace trace = inProgress[i];
                if (!inProgressHit[i] && trace.LastTime - time > FAIL_TIME_THRESHOLD / bpm) {
                    inProgress.Remove(trace);
                    missed.Add(trace);
                }
                else {
                    score += passed.TotalMinutes*bpm * 1000;
                    if (trace.LastTime < time) {
                        inProgress.Remove(trace);
                        completed.Add(trace);
                    }
                }
            }
            var toRemove = new List<RawNoteTrace>();
            foreach (RawNoteTrace trace in future) {
                if (trace.FirstTime < time - FAIL_TIME_THRESHOLD/bpm) {
                    toRemove.Add(trace);
                    missed.Add(trace);
                }
                else if (trace.FirstTime < time) {
                    foreach (float finger in fingers) {
                        if (Math.Abs(finger - trace.points[0].x) < DISTANCE_THRESHOLD) {
                            // hit!
                            score += 1000;
                            inProgress.Add(trace);
                            toRemove.Add(trace);
                            break;
                        }
                    }
                }
            }
            foreach (RawNoteTrace trace in toRemove) {
                future.Remove(trace);
            }

            var result = new CurrentNoteRendering();
            result.missed = new List<RawNoteTrace>();
            result.inProgress = new List<RawNoteTrace>();
            result.future = new List<RawNoteTrace>();
            foreach (RawNoteTrace trace in missed) {
                if (trace.LastTime > time - 10/bpm) {
                    result.missed.Add(trace);
                }
            }
            foreach (RawNoteTrace trace in inProgress) {
                result.inProgress.Add(trace.CutOff(time));
            }
            foreach (RawNoteTrace trace in future) {
                if (trace.FirstTime < time + 10/bpm) {
                    result.future.Add(trace);
                }
            }
            return result;
        }

        public void Initialize(NoteChart chart) {
            var rnt = new RawNoteTrace[chart.traces.Count];
            for (int i = 0; i < chart.traces.Count; i++) {
                NoteTrace trace = chart.traces[i];
                rnt[i] = chart.ToRaw(trace);
            }
            foreach (RawNoteTrace trace in rnt) {
                future.Add(trace);
            }
            bpm = chart.bpm;
        }
    }
}