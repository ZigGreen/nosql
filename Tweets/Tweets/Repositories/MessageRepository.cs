using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Linq.Mapping;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Serialization;
using Tweets.ModelBuilding;
using Tweets.Models;

namespace Tweets.Repositories
{
    public class MessageRepository : IMessageRepository
    {
        private readonly string connectionString;
        private readonly AppContext context;
        private readonly AttributeMappingSource mappingSource;
        private readonly IMapper<Message, MessageDocument> messageDocumentMapper;

        public MessageRepository(IMapper<Message, MessageDocument> messageDocumentMapper)
        {
            this.messageDocumentMapper = messageDocumentMapper;
            mappingSource = new AttributeMappingSource();
            connectionString = ConfigurationManager.ConnectionStrings["SqlConnectionString"].ConnectionString;
            context = new AppContext(connectionString);
        }

        public void Save(Message message)
        {
            var messageDocument = messageDocumentMapper.Map(message);
            context.Msgs.InsertOnSubmit(messageDocument);
            context.SubmitChanges();
            
        }

        public void Like(Guid messageId, User user)
        {
            if (context.Msgs.Count(x => x.Id == messageId) <= 0)
            {
                var exception = FormatterServices.GetUninitializedObject(typeof(SqlException))
                 as SqlException;
                throw exception;
            }
            var likeDocument = new LikeDocument {MessageId = messageId, UserName = user.Name, CreateDate = DateTime.UtcNow};
            context.Likes.InsertOnSubmit(likeDocument);
            context.SubmitChanges();
        }

        public void Dislike(Guid messageId, User user)
        {
            LikeDocument like;
            try
            {
                like = context.Likes.Where(l => l.UserName == user.Name && l.MessageId == messageId).First();
            }
            catch (InvalidOperationException ex)
            {
                return;
            }


            context.Likes.DeleteOnSubmit(like);
            context.SubmitChanges();

        }

        public IEnumerable<Message> GetPopularMessages()
        {
            var result = new List<Message>();
            var tmp = context.Likes.GroupBy(x => x.MessageId).OrderBy(x => x.Count())
                .Select(x => new
                {
                    Mid = x.Key,
                    count = x.Count()
                }
            );
            var query = from Msgs in context.Msgs
                        join likes in tmp on Msgs.Id equals likes.Mid into gj
                        from M_w_l in gj.DefaultIfEmpty()
                        select new { Id = Msgs.Id, CreateDate = Msgs.CreateDate, Text = Msgs.Text, Likes = (M_w_l == null ? 0 : M_w_l.count) };
            query = query.OrderByDescending(x => x.Likes).Take(10);
            foreach (var c_msg in query.ToList()) 
                {
                  
                    result.Add(new Message { Id = c_msg.Id, CreateDate = c_msg.CreateDate, Text = c_msg.Text , Likes = c_msg.Likes });
                }

            return result.ToArray();
            
        }

        public IEnumerable<UserMessage> GetMessages(User user)

        {
            


            var result = new List<UserMessage>();

            var msss = context.Msgs.Where(x => x.UserName.Equals(user.Name));
            foreach (var msg in msss)
            {
               var count = context.Likes.Count(x => x.MessageId.Equals(msg.Id));
               var liked = context.Likes.FirstOrDefault(x => x.MessageId.Equals(msg.Id) && x.UserName.Equals(user.Name));
               result.Add(new UserMessage { CreateDate = msg.CreateDate, Id = msg.Id, Liked = (liked != null ), Text = msg.Text, User = user, Likes = count });
            }



            return result.OrderByDescending(m => m.CreateDate);
        }
    }
}
