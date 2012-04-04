# Server Code for Computer Netoworks Course Project
# Karma
# Lists: ActiveUsers, FileHash_List, 
# Naved Alam | naved09028@iiitd.ac.in

from socket import *
from threading import *
import MySQLdb
from time import *

def SetupDB():
    ServerDB=MySQLdb.connect(host="localhost",user='root',passwd="alam",db="Karma")
    db_cursor=ServerDB.cursor()
    request="""CREATE TABLE users_list (
         Mac_Addr  CHAR(20) NOT NULL,
         IP_Addr  CHAR(20) NOT NULL,
         Index_hash CHAR(100) NOT NULL
         Nickname CHAR(20) NOT NULL,
         Active CHAR(1)) """
    try:
        db_cursor.cursor().execute(request)
        db_cursor.cursor().commit()
    except:
        db_cursor.cursor().rollback()
        print "random"
    request2="""CREATE TABLE user_file(
         file_hash  CHAR(100) NOT NULL,
         file_name  CHAR(100) NOT NULL,
         file_size  CHAR(100) NOT NULL,
         Mac_Addr   CHAR(20) NOT NULL) """
    try:
        db_cursor.cursor().execute(request2)
        db_cursor.cursor().commit()
    except:
        db_cursor.cursor().rollback()
    request3="""CREATE TABLE file_list(
         file_hash  CHAR(100) NOT NULL,
         kw  CHAR(20),
         
         ) """
    try:
        db_cursor.cursor().execute(request3)
        db_cursor.cursor().commit()
    except:
        db_cursor.cursor().rollback()

    request4="""CREATE TABLE pending_transfers (
         file_hash  CHAR(100) NOT NULL,
         Mac_Addr  CHAR(20) NOT NULL) """
    try:
        db_cursor.cursor().execute(request4)
        db_cursor.cursor().commit()
    except:
        db_cursor.cursor().rollback()
    request5="""CREATE TABLE relationships (
         Mac_Addr  CHAR(20) NOT NULL,
         Friend_MacAddr CHAR(20),
         Score CHAR(1)) """
    try:
        db_cursor.cursor().execute(request5)
        db_cursor.cursor().commit()
    except:
        db_cursor.cursor().rollback()

    request6="""CREATE TABLE download_request (
         Mac_Addr  CHAR(20) NOT NULL,
         file_hash CHAR(100)"""
    try:
        db_cursor.cursor().execute(request6)
        db_cursor.cursor().commit()

    except:
        db_cursor.cursor().rollback()

    return db_cursor



def sync(info_list):
    
    # to include the client to the users active list
    print "started shit"
    print info_list
    var=db_connect.cursor()
    var.execute ("""
    Select * from user_list where MAC_Addr='%s'""" % (info_list[0]))
    mac=var.fetchone()
    if(mac==None):
        
        db_connect.cursor().execute ("""
        INSERT INTO user_list (Mac_Addr, IP_Addr, Index_hash, Nickname, active)
        VALUES
        ('%s', '%s', '%s','%s','%s')""" %
        (info_list[0],info_list[1],info_list[2],info_list[3],info_list[4]))
        db_connect.commit()
        a=db_connect.cursor()
        a.execute ("""
        select * from user_list
        """)
        a1=a.fetchone()
        print a1
    else:
        db_connect.cursor().execute("""UPDATE user_list set IP_Addr = '%s', active='%s' where Mac_Addr='%s'""" % (info_list[1],info_list[4],mac[0]))
        db_connect.commit()
    log_file=open("ServerLog.txt","a")
    data=strftime("%m-%d-%y %H:%M:%S")
    text="Connection Established"
    # logging format Date, Time, Connection Established, Mac_Addr, IP_Addr
    final=data+" "+text+" "+info_list[0]+" "+info_list[1]+"\n"
    log_file.write(final)
    log_file.close()
   
    return info_list[0]

def create_ServerConnection(port):
    # to create a server connection
    serverSocket=socket(AF_INET,SOCK_STREAM)
    arg_addr=(gethostname(),port)
    serverSocket.bind(arg_addr)
    return serverSocket

def sync_files(clientSocket,addr,info, Mac_Addr):
    
    # the sync-up protocol to update files available for sharing by the active user
    print "sync_files"
    info_list=info.split('\n')
    print info_list
    try:
        db_connect.cursor().execute("""DELETE from user_file where Mac_Addr='%s' """ %(Mac_Addr))
        db_connect.commit()
    except:
        db_connect.connection.rollback()
    info_list.pop(-1)
    for i in info_list:
        data=i.split("|")
        print data
        keywords=data[3:]
        var=db_connect
        print data[0],data[1],data[2],Mac_Addr
        # var.execute("""INSERT INTO user_file (file_hash,file_name,file_size,Mac_Addr) VALUES('21', '12', '1'),('22', '13', '2'),('23', '14', '3'),('24', '15', '4')""")



        var.cursor().execute("""INSERT INTO user_file (file_hash,file_name,file_size, Mac_Addr) VALUES ('%s','%s','%s','%s') """ %(str(data[0]),str(data[1]),str(data[2]),str(Mac_Addr)))
        print "inserted",var.cursor().rowcount
        var.commit()
        for j in keywords:
            var2=db_connect    
            var2.cursor().execute("""INSERT INTO file_list (file_hash,kw) VALUES ('%s','%s')"""
                                       %(data[0],j))
            var2.commit()
        
    return None
    

def ready(clientSocket, addr,Mac_Addr):
    # the client can now query and request for IPs of peers
    # the client is sent a list of file_hash requests
    
    var=db_connect.cursor()
    var.execute("""SELECT * from download_request where Mac_Addr='%s'""" %(Mac_Addr))
    result_hash=var.fetchall()
    final_hash="Download"
    """for i in result_hash:
        final_hash=final_hash+"|"+i[1]
    clientSocket.send(final_hash)"""
    
    while(1):
        file_hash=""
        Mac_Addr=""
        request=clientSocket.recv(4096)
        print request
        try:
            if(len(request)!=0):
                request_data=request.split('|')
                print " data has been split"

                if(request_data[0]=="SEARCH"):
                    print "Search Query"    
                    IP_list=[]
                    kw_list=request_data[1:]
                    var=db_connect.cursor()
                    
                    
                    
                    hash_all=[]
                    
                    for i in kw_list:
                        
                        tempkw=str('%'+i+'%')
                        print tempkw
                        var.execute("""SELECT DISTINCT file_hash from file_list where kw like '%s'""" %tempkw)
                        #var.execute("""SELECT file_hash from file_list where kw = %s""" %(kw))
                        temp_list=var.fetchall()
                        print temp_list
                        hash_all.extend(temp_list)
                        #print "dh"
                    print "outta loop"
                    print (len(hash_all))
                    if(len(hash_all)!=0):
                        hash_list=[]
                        for k in hash_all:
                            hash_list.append(hash_all[0])
                        ID_list=[]
                        Mac_Addr_list=[]
                        hash_deep=[]
                        for i in hash_list:
                            hash_deep.append(i)
                            print "returning results"
                        for i in hash_list:
                            try:
                                var2=db_connect.cursor()
                                var2.execute("""SELECT * from user_file
                                    where file_hash='%s'"""%(i))
                                print "BEFORE FETCHALL"
                                ID_list.extend(var2.fetchall())
                                

                                hash_deep.remove(i) # list of non active users
                            except:
                                db_connect.connection.rollback()
                            print ID_list
                            for j in ID_list:
                                print j
                                
                                Mac_Addr_list.append(j[3])
                                print Mac_Addr_list
                                print j[3]
                            
                        #hash_deep has non-active users

                        IP_list=[]
                        var_new=db_connect.cursor()
                        print len(Mac_Addr_list)
                        for k in Mac_Addr_list:

                            var_new.execute("""SELECT IP_Addr, Nickname from user_list
                                where Mac_Addr='%s'"""%(k))
                            IP_list.append(var_new.fetchone())
                                
                                
                        print IP_list
                        final_result=""
                        print len(ID_list)
                        for b in range(0,len(ID_list)):
                            # filehash | filename | filesize | peer ip| peernick
                            print "ENTERED LOOP"
                            print ID_list
                            print IP_list
                            
                            element=(str(ID_list[b][0])+"|"
                                     +str(ID_list[b][1])+"|"+
                                     str(ID_list[b][2])+"|"+
                                     str(IP_list[b][0])+"|"+
                                     str(IP_list[b][1]))
                            print "element computed"
                            if(IP_list[b][0]=='0.0.0.0'):
                                element=element+"|"+"OFFLINE"+"\n"
                            else:
                                element=element+"|"+"ONLINE"+"\n"
                                
                            unique_offline_hash=[]
                            print element
                            for i in hash_deep:
                                if i not in unique_offline_hash:
                                    unique_offline_hash.append(i)
                            print("HERE")    
                            '''if ID_list[b][3] in unique_offline_hash:
                                element=(str(ID_list[b][0])+"|"+
                                         str(ID_list[b][1])+"|"+
                                         str(ID_list[b][2])+"|"+
                                         "0.0.0.0"+"|"+"Inactive Peer"+"|"+"OFFLINE"+"\n")'''
                            final_result=final_result+element   
                        print "results computed"

                        final_result=final_result+"END OF RESULTS"
                        print "results computed"
                        clientSocket.send(final_result)

                    else:
                        print "random"
                        #clientSocket.send("No Results found for te query") # to be discussed
                        clientSocket.send("END OF RESULTS")
            
                if(request_data[0]=="REQUEST"):
                    file_hash=request_data[1]
                    print file_hash
                    db_connect.cursor().execute("""INSERT INTO pending_transfers (file_hash, Mac_Addr) VALUES ('%s','%s') """ %(file_hash,request_data[2]))
                    db_connect.commit()
                
                    
                
            # clean up the database--> user_list
        except:
            log_file=open("ServerLog.txt","a")
            data=strftime("%m-%d-%y %H:%M:%S")
            text="Connection Failed"
            # logging format Date, Time, Connection Failed, Mac_Addr, IP_Addr
            final=data+" "+text+" "+Mac_Addr+" "+addr[0]+"\n"
            log_file.write(final)
            log_file.close()
           
            # update all user list tables for broken connection
            db_connect.cursor().execute("""UPDATE user_list set IP_Addr='%s', Active='%s' where IP_Addr='%s'""" %('0.0.0.0','0',addr[0]))
            db_connect.commit()
            # to update pending transfers
            # adding this transfer to pending transfers
            
            break  # to get out of the infinite loop
            
            
    return None


def setupClient(addr, clientSocket):
   
    """expception handling
    date|time| MAC Logging"""
    print "client thread started"
    information=clientSocket.recv(4096)
    print "wating"
    info=information.split('|')
    print "info has just been split"
    # MAC_Addr, IP_Addr, Index_hash, Nickname, Active
    info_list=[str(info[2]),str(addr[0]),str(info[3]),str(info[1]),str(1)]
    print info_list
    Mac_Addr=sync(info_list) # returns Mac_Addr of the peer just connected
    '''a=db_connect.cursor()
    print "final"
    a.execute("""SELECT Index_hash FROM user_list where
                       Mac_Addr='%s'""" % (Mac_Addr))
    hash_t=a.fetchall()
    hash_t=hash_t[0]
    print "FETCHALL WORKS"
    #hash_t=db_connect.fetchone()
    #print hash_t[0]
    if(len(hash_t)==0):
        clientSocket.send("AAAAAAAAAAAAAAAAAAAAA")
        print"rfgghjk;g"
    '''
    #clientSocket.send(hash_t[0])
    print "chudaap"   # Server sending hash_index file
    #new_hash=clientSocket.recv(4096)  # Server received hash_index from the client
    #new_hash=str(info[3])
    index_new = clientSocket.recv(4096)
    sync_files(clientSocket,addr,index_new,Mac_Addr)
    ready(clientSocket,addr,Mac_Addr)
    ''' if(new_hash==hash_t[0]):
        #print 
        ready(clientSocket,addr,Mac_Addr)
        #to be in a ready state to accept queries
    else:
        
        index_new=clientSocket.recv(4096)
        # Sending updated new file indexes
        sync_files(clientSocket, addr, index_new, Mac_Addr)
        #to syn up the file lists the protocol part..!
        ready(clientSocket,addr,Mac_Addr)
        # to be in a ready state to accept queries
    '''   
    return None

def send_data(IP_Addr, file_hash,Mac_Addr):
    #sub-part of pending transfers
    # decide upon a port number
    port=8002
    clientSocket=socket(AF_INET, SOCK_STREAM)
    clientSocket.connect((IP_Addr, port))
    try:
        clientSocket.send("Online"+"|"+IP_Addr+"|"+file_hash+"|"+"END")
        try:
            db_connect.cursor().execute("""INSERT INTO download_request VALUES(Mac_Addr, file_hash) ('%s','%s')"""
                               %(Mac_Addr,file_hash))
            db_connect.commit()
        except:
            db_connect.connection.rollback()
        
    except:
        log_file=open("ServerLog.txt","a")
        data=strftime("%m-%d-%y %H:%M:%S")
        text="Connection Failed"
        # logging format Date, Time, Connection Failed, Mac_Addr, IP_Addr
        final=data+" "+text+" "+info_list[0]+" "+info_list[1]+"\n"
        log_file.write(info_list)
        log_file.close()

    return None
    

def pending_transfers():
    # to handle all the pending transfers
    # to give delay tolerance to the code
    # to work on round robin 
    ctr=0
    while 1:
        ctr=ctr+1
        try:
            db_connect.cursor().execute("""SELECT rownumber, Mac_Addr, Friend_Mac_Addr from (SELECT *,@rownum:=@rownum+1 as rownumber from pending_transfers
            , (SELECT @rownum:=0)r)as ID where rownumber='%s'""" %(ctr))
        except:
            db_connect.connection.rollback()
        try:
            result=db_connect.cursor().fetchone()
        except:
            db_connect.connection.rollback()
        fil_hash=result[2]
        Mac_Addr_prime=result[1]
        try:
            # Select all online Friend Users using an innner join query
            db_connect.cursor().execute("""SELECT Mac_Addr, Friend_MacAddr, Score, IP_Addr from relationships INNER JOIN user_list
                                on relationships.Mac_Addr=user_list.Mac_Addr order by relationship.Score DESC""")
        except:
            db_connect.connection.rollback()
        try:
            friends=db_connect.cursor().fetchall()
            if(len(friends)>3):
                final=[]
                for i in range(0,3):
                    final.append(friends[i])
            for i in final:
                try:
                    db_connect.cursor().execute("""SELECT IP_Addr from user_list where Mac_Addr='%s'""" %(i) )
                except:
                    db_connect.connection.rollback()
                try:
                    db_connect.cursor().fetchone()
                except:
                    db_connect.rollback()
            # all active friends in friend list
        except:
            db_connect.connection.rollback()
            
        try:
            db_connect.cursor().execute("""SELECT * from (SELECT file_hash,file_name,file_size,Mac_addr,IP_Addr,Nickname from user_list INNER JOIN user_file
                                where user_file.Mac_Addr=user_list.Mac_Addr )as avail_files where file_hash='%s'"""%(file_hash))
            ID_list=db_connect.cursor().fetchall()
        except:
            db_connect.connection.rollback()

        for j in ID_list:
            Mac_Addr_list.append(j(3))
            IP_list=[]
            for k in Mac_Addr_list:
                try:
                    # all active user for each unique file hash
                    db_connect.cursor().execute("""SELECT IP_Addr, Nickname from user_list where Mac_Addr='%s'"""%(k))
                    IP_list.append(db_connect.cursor().fetchone())
                        
                        
                except:
                    db_connect.connection.rollback()
                final_result=""
                
                # filehash | filename | filesize | peer ip| peernick
                
                for b in range(0,len(ID_list)):
                    
                    element=("Online"+"|"+str(ID_list[b][0])+"|"
                         +str(ID_list[b][1])+"|"+
                         str(ID_list[b][2])+"|"+
                         str(ID_list[b][4])+"|"+
                         str(ID_list[b][5])+"|"+
                         "END")
                final_result=final_result+element
                # thread to start a TCP connection and send the data
                for i in ID_list:
                    clientThread=Thread(target=send_data,args=[ID_list[i][4], ID_list[i][0],Mac_Addr_prime])
                    clientThread.start()

            
    
    return None
    
def friend_add(Mac_Addr_1, Mac_Addr_2):
    try:
        db_connect.cursor().execute("""SELECT * from relationships
                            where Mac_Addr=%s and Friend_MacAddr=%s"""
                           %(Mac_Addr_1, Mac_Addr_2))
        try:
            results=db_connect.cursor().fetchone()
            if (a==None):
                try:
                    db_connect.cursor().execute("""INSERT INTO relationships (
                    Mac_Addr, Friend_MacAddr, Score) VALUES
                    ('%s', '%s','%s') """ %(Mac_Addr_1, Mac_Addr_2,'1'))
                    db_connect.commit()
                except:
                    db_connect.connection.rollback()
            else:
                try:
                    db_connect.cursor().execute("""UPDATE relationships set Score='%s' where
                    Mac_Addr=%s and Friend_Addr=%s """ %(str(int(results[2])+1)))
                    db_connect.commit()
                except:
                    db_connect.connection.rollback()
        except:
            db_connect.connection.rollback()
    except:
        db_connect.connection.rollback()
    try:
        db_connect.cursor().execute("""SELECT * from relationships
                            where Mac_Addr='%s' and Friend_MacAddr='%s'"""
                           %(Mac_Addr_2, Mac_Addr_1))
        try:
            result=db_connect.cursor().fetchone()
            if (a==None):
                try:
                    db_connect.cursor().execute("""INSERT INTO relationships (Mac_Addr, Friend_MacAddr, Score) VALUES
                    ('%s', '%s','%s') """ %(Mac_Addr_2, Mac_Addr_1,'1'))
                    db_connect.commit()
                except:
                    db_connect.connection.rollback()
            else:
                try:
                    db_connect.cursor().execute("""UPDATE relationships set Score='%s' where
                    Mac_Addr='%s' and Friend_Addr='%s' """ %(str(int(result[2])+1)))
                    db_connect.commit()
                except:
                    db_connect.connection.rollback()
        except:
            db_connect.connection.rollback()
    except:
        db_connect.connection.rollback()
    return None
    
def calc_friendship(Mac_Addr):
    db_connect.cursor().execute("""SELECT * from user_list where Mac_Addr!='%s' """ %(Mac_Addr))
    results=db_connect.cursor().fetchall()
    for i in results:
        friend_add(Mac_Addr,i[0])
    return None


db_connect=MySQLdb.connect (host = "localhost",
                       user = "root",
                       passwd = "alam",
                       db = "Karma")           
port=8005
serverSocket=create_ServerConnection(port)
try:
    
    # The number of clients that can connect to the server is 100
    serverSocket.listen(100)
except:
    serverSocket.close()

clientSocket, client_addr=serverSocket.accept()
clientThread=Thread(target=setupClient,args=[client_addr, clientSocket])
clientThread.start()

#pending=Thread(target=pending_transfers)
#pending.start()

while 1:
    clientSocket, client_addr=serverSocket.accept()
    clientThread=Thread(target=setupClient,args=[client_addr, clientSocket])
    clientThread.start()



# thread to initiate pending requests and subsequent threads for initiating those requests
serverSocket.close()



    

