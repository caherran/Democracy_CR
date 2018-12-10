using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using CrystalDecisions.CrystalReports.Engine;
using Democracy_CR.Models;
using Microsoft.Reporting.WebForms;

namespace Democracy_CR.Controllers
{
    public class VotingsController : Controller
    {
        private DemocracyContext db = new DemocracyContext();

        [Authorize(Roles = "User")]
        public ActionResult ShowResults(int id)
        {
            var report = this.GenerateResultReport(id);
            var stream = report.ExportToStream(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat);
            return File(stream, "application/pdf");
        }

        private ReportClass GenerateResultReport(int id)
        {
            var dataTable = GenerateData(id);

            var report = new ReportClass();
            report.FileName = Server.MapPath("/Reports/Results.rpt");
            report.Load();
            report.SetDataSource(dataTable);

            return report;
        }

        private DataTable GenerateData(int id)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            var connection = new SqlConnection(connectionString);
            var dataTable = new DataTable();
            var sql = @"SELECT dbo.Votings.VotingId, 
	                           dbo.Votings.Description AS Voting, 
	                           dbo.States.Description AS State, 
	                           dbo.Users.FirstName + ' ' + dbo.Users.LastName AS Candidate, 
	                           dbo.Candidates.QuantityVotes
                        FROM dbo.Candidates INNER JOIN
                             dbo.Users ON dbo.Candidates.UserId = dbo.Users.UserId INNER JOIN
                             dbo.Votings ON dbo.Candidates.VotingId = dbo.Votings.VotingId INNER JOIN
                             dbo.States ON dbo.Votings.StateId = dbo.States.StateId
                       WHERE Votings.VotingId = " + id;

            try
            {
                connection.Open();
                var command = new SqlCommand(sql, connection);
                var adapter = new SqlDataAdapter(command);
                adapter.Fill(dataTable);

            }
            catch (Exception ex)
            {
                ex.ToString();
            }

            return dataTable
;
        }

        [Authorize(Roles = "User")]
        public ActionResult ReportResults(int id, string type, string fileName)
        {
            LocalReport lr = new LocalReport();
            string path = Path.Combine(Server.MapPath("~/Reports"), "Results.rdlc");
            if (System.IO.File.Exists(path))
            {
                lr.ReportPath = path;
            }
            else
            {
                return View("Results");
            }

            var dataTable = GenerateData(id);

            ReportDataSource source = new ReportDataSource("DS_Results", dataTable);
            lr.DataSources.Add(source);
            string reportType = type;
            string mimeType;
            string encoding;
            string fileNameExtension;

            string deviceInfo = "" +
                "<DeviceInfo>" +
                "   <OutputFormat>" + type + "</OutputFormat>" +
                "   <PageWidth>8.5in</PageWidth>" +
                "   <PageHeight>11in</PageHeight>" +
                "   <MarginTop>0.5in</MarginTop>" +
                "   <MarginLeft>1in</MarginLeft>" +
                "   <MarginRight>1in</MarginRight>" +
                "   <MarginBottom>0.5in</MarginBottom>" +
                "</DeviceInfo>";

            Warning[] warnings;
            string[] streams;
            byte[] renderedBytes;

            renderedBytes = lr.Render(
                reportType,
                deviceInfo,
                out mimeType,
                out encoding,
                out fileNameExtension,
                out streams,
                out warnings
                );

            return File(renderedBytes, mimeType/*, fileName*/);
        }

        [Authorize(Roles = "User")]
        public ActionResult Results()
        {
            var votings = db.Votings.Include(v => v.State);
            return View(votings.ToList());
        }

        [Authorize(Roles = "User")]
        public ActionResult VoteForCandidate(int candidateId, int votingId)
        {
            var user = db.Users
                        .Where(u => u.UserName == this.User.Identity.Name)
                        .FirstOrDefault();

            if(user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var candidate = db.Candidates
                        .Find(candidateId);

            if (candidate == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var voting = db.Votings
                        .Find(votingId);

            if (voting == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if(this.VoteCandidate(user, candidate, voting))
            {
                return RedirectToAction("MyVotings");
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        private bool VoteCandidate(User user, Candidate candidate, Voting voting)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                var votingDetail = new VotingDetail
                {
                    CandidateId = candidate.CandidateId,
                    DateTime = DateTime.Now,
                    UserId = user.UserId,
                    VotingId = voting.VotingId,
                };

                db.VotingDetails.Add(votingDetail);

                candidate.QuantityVotes++;
                db.Entry(candidate).State = EntityState.Modified;

                voting.QuantityVotes++;
                db.Entry(voting).State = EntityState.Modified;

                try
                {
                    db.SaveChanges();
                    transaction.Commit();
                    return true;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                }
            }
            return false;
        }

        [Authorize(Roles = "User")]
        public ActionResult Vote(int id)
        {
            var voting = db.Votings.Find(id);

            var view = new VotingVoteView
            {
                DateTimeEnd = voting.DateTimeEnd,
                DateTimeStart = voting.DateTimeStart,
                Description = voting.Description,
                IsEnabledBlankVote = voting.IsEnabledBlankVote,
                IsForAllUsers = voting.IsForAllUsers,
                MyCandidates = voting.Candidates.ToList(),
                Remarks = voting.Remarks,
                VotingId = voting.VotingId,
            };

            return View(view);
        }

        [Authorize(Roles = "User")]
        public ActionResult MyVotings()
        {
            var user = db.Users.Where(u => u.UserName == this.User.Identity.Name).FirstOrDefault();

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "There is an error with the current user, call support");
                return View();
            }


            //Get event votings for the current time
            var state = this.GetState("Open");

            var votings = db.Votings
                            .Where(v =>
                                    v.StateId == state.StateId &&
                                    v.DateTimeStart <= DateTime.Now &&
                                    v.DateTimeEnd >= DateTime.Now)
                            .Include(v => v.Candidates)
                            .Include(v => v.VotingGroups)
                            .Include(v => v.State)
                            .ToList();

            //Discard events where user already vote
            foreach (var voting in votings.ToList())
            {
                var votingDetail = db.VotingDetails
                                    .Where(vd => vd.VotingId == voting.VotingId &&
                                                 vd.UserId == user.UserId)
                                    .FirstOrDefault();

                if (votingDetail != null)
                {
                    votings.Remove(voting);
                }
            }

            //Discard events by groups where user is not included
            foreach (var voting in votings.ToList())
            {
                if (!voting.IsForAllUsers)
                {
                    bool userBelongsToGroup = false;

                    foreach (var votingGroup in voting.VotingGroups)
                    {
                        var userGroup = votingGroup.Group.GroupMembers
                                        .Where(gm => gm.UserId == user.UserId)
                                        .FirstOrDefault();

                        if (userGroup != null)
                        {
                            userBelongsToGroup = true;
                            break;
                        }
                    }

                    if (!userBelongsToGroup)
                    {
                        votings.Remove(voting);
                    }
                }
            }

            return View(votings);
        }

        private State GetState(string stateName)
        {
            var state = db.States
                        .Where(s => s.Description == stateName)
                        .FirstOrDefault();

            if (state == null)
            {
                state = new State
                {
                    Description = stateName,
                };

                db.States.Add(state);
                db.SaveChanges();
            }

            return state;
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Index()
        {
            var votings = db.Votings.Include(v => v.State);
            return View(votings.ToList());
        }

        [Authorize(Roles = "Admin")]
        public ActionResult AddGroup(int id)
        {
            ViewBag.GroupId = new SelectList(db.Groups.OrderBy(g => g.Description), "GroupId", "Description");

            var view = new VotingGroup
            {
                VotingId = id,
            };

            return View(view);
        }

        [HttpPost]
        public ActionResult AddGroup(VotingGroup view)
        {
            if (ModelState.IsValid)
            {
                var votingGroup = db.VotingGroups.Where(g => g.VotingId == view.VotingId && g.GroupId == view.GroupId).FirstOrDefault();

                if (votingGroup != null)
                {
                    ModelState.AddModelError(string.Empty, "The group already belong to voting");

                    //ViewBag.Error = "The group already belong to voting";
                    ViewBag.GroupId = new SelectList(db.Groups.OrderBy(g => g.Description), "GroupId", "Description");
                    return View(view);
                }

                votingGroup = new VotingGroup
                {
                    GroupId = view.GroupId,
                    VotingId = view.VotingId,
                };

                db.VotingGroups.Add(votingGroup);
                db.SaveChanges();
                return RedirectToAction(string.Format("Details/{0}", view.VotingId));
            }

            ViewBag.GroupId = new SelectList(db.Groups.OrderBy(g => g.Description), "GroupId", "Description");

            return View(view);
        }

        [Authorize(Roles = "Admin")]
        public ActionResult DeleteGroup(int id)
        {
            var votingGroup = db.VotingGroups.Find(id);
            if (votingGroup != null)
            {
                db.VotingGroups.Remove(votingGroup);
                db.SaveChanges();
            }
            return RedirectToAction(string.Format("Details/{0}", votingGroup.VotingId));
        }

        [Authorize(Roles = "Admin")]
        public ActionResult AddCandidate(int id)
        {
            var view = new AddCandidateView
            {
                VotingId = id,
            };

            ViewBag.UserId = new SelectList(db.Users
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName), "UserId", "FullName");

            return View(view);
        }

        [HttpPost]
        public ActionResult AddCandidate(AddCandidateView view)
        {
            if (ModelState.IsValid)
            {
                var candidate = db.Candidates.Where(c => c.VotingId == view.VotingId && c.UserId == view.UserId).FirstOrDefault();

                if (candidate != null)
                {
                    ModelState.AddModelError(string.Empty, "The candidate already belong to voting");

                    ViewBag.UserId = new SelectList(db.Users
                            .OrderBy(u => u.FirstName)
                            .ThenBy(u => u.LastName), "UserId", "FullName");
                    return View(view);
                }

                candidate = new Candidate
                {
                    UserId = view.UserId,
                    VotingId = view.VotingId,
                };

                db.Candidates.Add(candidate);
                db.SaveChanges();
                return RedirectToAction(string.Format("Details/{0}", view.VotingId));
            }

            ViewBag.UserId = new SelectList(db.Users
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName), "UserId", "FullName");

            return View(view);
        }

        [Authorize(Roles = "Admin")]
        public ActionResult DeleteCandidate(int id)
        {
            var candidate = db.Candidates.Find(id);
            if (candidate != null)
            {
                db.Candidates.Remove(candidate);
                db.SaveChanges();
            }
            return RedirectToAction(string.Format("Details/{0}", candidate.VotingId));
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Voting voting = db.Votings.Find(id);
            if (voting == null)
            {
                return HttpNotFound();
            }

            var view = new DetailsVotingView
            {
                Candidates = voting.Candidates.ToList(),
                CandidateWinId = voting.CandidateWinId,
                DateTimeEnd = voting.DateTimeEnd,
                DateTimeStart = voting.DateTimeStart,
                Description = voting.Description,
                IsEnabledBlankVote = voting.IsEnabledBlankVote,
                IsForAllUsers = voting.IsForAllUsers,
                QuantityBlankVotes = voting.QuantityBlankVotes,
                QuantityVotes = voting.QuantityVotes,
                Remarks = voting.Remarks,
                StateId = voting.StateId,
                VotingGroups = voting.VotingGroups.ToList(),
                VotingId = voting.VotingId,
            };

            return View(view);
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Create()
        {
            /*var list = db.States.ToList();
            list.Add(new State { StateId = 0, Description = "[Select a State...]" });
            list = list.OrderBy(c => c.Description).ToList();
            ViewBag.StateId = new SelectList(list, "StateId", "Description");*/
            ViewBag.StateId = new SelectList(db.States, "StateId", "Description");
            var view = new VotingView
            {
                //DateStart = DateTime.ParseExact(DateTime.Now.ToShortDateString(), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                //DateStart = DateTime.Now,
                //TimeStart = new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second),
                //DateEnd = DateTime.ParseExact(DateTime.Now.ToShortDateString(), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                //DateEnd = DateTime.Now,
                //TimeEnd = new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second),
            };
            return View(view);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(VotingView view)
        {
            //var stateId = int.Parse(Request["StateId"]);
            //if (stateId == 0)
            //{
            //    var list = db.States.ToList();
            //    list.Add(new State { StateId = 0, Description = "[Select a State...]" });
            //    list = list.OrderBy(c => c.Description).ToList();
            //    ViewBag.StateId = new SelectList(list, "StateId", "Description");
            //    ViewBag.Error = "You must select a state";

            //    return View(voting);
            //}

            if (ModelState.IsValid)
            {
                var voting = new Voting
                {
                    DateTimeEnd = view.DateEnd
                                      .AddHours(view.TimeEnd.Hour)
                                      .AddMinutes(view.TimeEnd.Minute),
                    DateTimeStart = view.DateStart
                                        .AddHours(view.TimeStart.Hour)
                                        .AddMinutes(view.TimeStart.Minute),
                    Description = view.Description,
                    IsEnabledBlankVote = view.IsEnabledBlankVote,
                    IsForAllUsers = view.IsForAllUsers,
                    Remarks = view.Remarks,
                    StateId = view.StateId,
                };

                db.Votings.Add(voting);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.StateId = new SelectList(db.States, "StateId", "Description", view.StateId);
            return View(view);
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var voting = db.Votings.Find(id);
            if (voting == null)
            {
                return HttpNotFound();
            }

            //DateTimeFormatInfo usDtfi = new CultureInfo("es-CO", false).DateTimeFormat;

            var view = new VotingView
            {
                DateEnd = voting.DateTimeEnd/*Convert.ToDateTime(voting.DateTimeEnd.ToShortDateString(), usDtfi)*/,
                DateStart = voting.DateTimeStart/*Convert.ToDateTime(voting.DateTimeStart.ToShortDateString(), usDtfi)*/,
                Description = voting.Description,
                IsEnabledBlankVote = voting.IsEnabledBlankVote,
                IsForAllUsers = voting.IsForAllUsers,
                Remarks = voting.Remarks,
                StateId = voting.StateId,
                TimeEnd = voting.DateTimeEnd,
                TimeStart = voting.DateTimeStart,
                VotingId = voting.VotingId,
            };

            ViewBag.StateId = new SelectList(db.States, "StateId", "Description", voting.StateId);

            return View(view);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(VotingView view)
        {
            if (ModelState.IsValid)
            {
                var voting = new Voting
                {
                    DateTimeEnd = view.DateEnd
                                      .AddHours(view.TimeEnd.Hour)
                                      .AddMinutes(view.TimeEnd.Minute),
                    DateTimeStart = view.DateStart
                                        .AddHours(view.TimeStart.Hour)
                                        .AddMinutes(view.TimeStart.Minute),
                    Description = view.Description,
                    IsEnabledBlankVote = view.IsEnabledBlankVote,
                    IsForAllUsers = view.IsForAllUsers,
                    Remarks = view.Remarks,
                    StateId = view.StateId,
                    VotingId = view.VotingId,
                };

                db.Entry(voting).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.StateId = new SelectList(db.States, "StateId", "Description", view.StateId);
            return View(view);
        }

        [Authorize(Roles = "Admin")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Voting voting = db.Votings.Find(id);
            if (voting == null)
            {
                return HttpNotFound();
            }
            return View(voting);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Voting voting = db.Votings.Find(id);

            try
            {
                db.Votings.Remove(voting);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null &&
                   ex.InnerException.InnerException != null &&
                   ex.InnerException.InnerException.Message.Contains("REFERENCE"))
                {
                    ViewBag.Error = "Can't delete record, has related records";
                }
                else
                {
                    ViewBag.Error = ex.Message;
                }

                return View(voting);
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
