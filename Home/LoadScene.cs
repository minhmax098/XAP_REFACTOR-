using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Linq;

namespace Home
{
    public class LoadScene : MonoBehaviour
    {
        public GameObject waitingScreen;
        public GameObject contentItemCategoryWithLesson;
        public GameObject contentItemCategory;
        public GameObject searchBox;
        // add 3 record: searchBtn, xBtn, sumLesson
        public GameObject searchBtn;
        public GameObject xBtn;
        private ListXRLibrary x;
        private int numberCategories;
        public ScrollRect scroll; 

        private Queue<(UnityWebRequest, GameObject)> RequestsAndItems = new Queue<(UnityWebRequest, GameObject)>();  // Contains all RequestsAndItem
        private List<(UnityWebRequestAsyncOperation, GameObject)> pool = new List<(UnityWebRequestAsyncOperation, GameObject)>();
        private int POOL_SIZE = 15;
        private int calculatedSize = 20; 

        void Start()
        {
            waitingScreen.SetActive(false);
            Screen.orientation = ScreenOrientation.Portrait;
            StatusBarManager.statusBarState = StatusBarManager.States.TranslucentOverContent;
            StatusBarManager.navigationBarState = StatusBarManager.States.Hidden;
            // search record
            searchBtn.SetActive(true);
            xBtn.SetActive(false);
            xBtn.transform.GetComponent<Button>().onClick.AddListener(ClearInput);
            // searchBox.GetComponent<InputField>().onValueChanged.AddListener(UpdateList); 
            // LoadCategories();
            StartCoroutine(LessonByCategory());
        }
        
        void Update()
        {
            if (searchBox.GetComponent<InputField>().isFocused == true)
            {
                Debug.Log("Search box is focused: ");
                PlayerPrefs.SetString("user_input", "");

                StartCoroutine(LoadAsynchronously(SceneConfig.xrLibrary_edit));
            }
            
            if (!pool.Any())
            {
                // pool is empty
                int remain = POOL_SIZE;
                if (RequestsAndItems.Count > 0 && remain > 0)
                {
                    remain -= 1;
                    var temp = RequestsAndItems.Dequeue();
                    pool.Add((temp.Item1.SendWebRequest(), temp.Item2));
                }
            }
            if (!pool.Any() == false)
            {
                if(AllRequestDone(pool))
                {
                    HandleAllRequestsWhenFinished(pool);
                    pool.Clear();
                }
            }
        }
       
        void scrollContent(string id)
        {
            GameObject currentBtnObj = scroll.transform.Find("Viewport/Content/" + id).gameObject;
            Debug.Log("Scroll content active: ");
            Debug.Log("Value: " + (float)(currentBtnObj.transform.GetSiblingIndex() + 1) / numberCategories);
            scroll.verticalNormalizedPosition = 1f;
            scroll.verticalNormalizedPosition = 1f - (float)(currentBtnObj.transform.GetSiblingIndex() + 1) / numberCategories + 0.05f;
        }

        void ClearInput()
        {
            searchBox.GetComponent<InputField>().SetTextWithoutNotify(""); 
            xBtn.SetActive(false);
            searchBtn.SetActive(true);
        }

        IEnumerator LessonByCategory()
        {
            x = LoadData.Instance.GetCategoryWithLesson();
            numberCategories = x.data.Length;
            
            // load from file json
            foreach (OrganForHome organ in x.data)
            {
                // check if u have a lesson, load image and if u don't have a lesson, not load 
                if (organ.listLesson.Length > 0)
                {
                    GameObject itemCategoryObject = Instantiate(Resources.Load(DemoConfig.demoItemCategoryWithLessonPath) as GameObject);
                    itemCategoryObject.name = organ.organsId.ToString();
                    itemCategoryObject.transform.GetChild(0).GetChild(0).gameObject.GetComponent<Text>().text = organ.organsName;
                    itemCategoryObject.transform.parent = contentItemCategoryWithLesson.transform;
                    itemCategoryObject.transform.localScale = Vector3.one;
                    Button moreLessonBtn = itemCategoryObject.transform.GetChild(0).GetChild(1).gameObject.GetComponent<Button>();
                    moreLessonBtn.onClick.AddListener(() => updateOrganManager(organ.organsId, organ.organsName));
                    GameObject subContent = itemCategoryObject.transform.GetChild(1).GetChild(0).GetChild(0).gameObject;

                    foreach (LessonForHome lesson in organ.listLesson)
                    {
                        string imageUri = String.Format(APIUrlConfig.LoadLesson, lesson.lessonThumbnail);
                        var www = UnityWebRequestTexture.GetTexture(imageUri); 
                        // UI component for one Lesson
                        GameObject lessonObject = Instantiate(Resources.Load(DemoConfig.demoLessonObjectPath) as GameObject);
                        lessonObject.name = lesson.lessonId.ToString();
                        lessonObject.transform.GetChild(1).GetChild(0).gameObject.GetComponent<Text>().text = Helper.FormatString(lesson.lessonTitle, calculatedSize);
                        lessonObject.transform.parent = subContent.transform;
                        Button lessonBtn = lessonObject.GetComponent<Button>();
                        lessonBtn.onClick.AddListener(() => InteractionUI.Instance.onClickItemLesson(lesson.lessonId)); 
                        RequestsAndItems.Enqueue((www, lessonObject));
                    }
                }   
            }
            yield return null;  
        }

        // Update the UI 
        void LateUpdate()
        {
            foreach (var item in RequestsAndItems)
            {
                // if (organLesson.Count > 0)
                // {
                //     for (var i = 0; i < organLesson.Count; i++)
                //     {
                //         organLesson[i].GetComponent<RectTransform>().localScale = Vector3.one;
                //     }
                // }
                item.Item2.GetComponent<RectTransform>().localScale = Vector3.one;
            }
        }

        private bool AllRequestDone(List<(UnityWebRequestAsyncOperation, GameObject)> requests)
        {
            return requests.All(r => r.Item1.isDone); 
        }

        private void HandleAllRequestsWhenFinished(List<(UnityWebRequestAsyncOperation, GameObject)> requests)
        {
            for(var i = 0; i < requests.Count; i++)
            {
                var www = requests[i].Item1.webRequest;
                if(www.isNetworkError || www.isHttpError) 
                {
                    // Don't modify any thing
                }
                else 
                {
                    Texture2D tex = ((DownloadHandlerTexture) www.downloadHandler).texture;
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(tex.width / 2, tex.height / 2));
                    // Change the image with the specific item
                    requests[i].Item2.transform.GetChild(0).GetChild(0).gameObject.GetComponent<Image>().sprite = sprite;  
                    requests[i].Item2.transform.GetChild(2).gameObject.SetActive(false);
                }
            }
        }

        void updateOrganManager(int id, string name)
        {
            OrganManager.InitOrgan(id, name);
            Debug.Log(id);
            StartCoroutine(LoadAsynchronously(SceneConfig.listOrgan_edit));
        }

        public IEnumerator LoadAsynchronously(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            waitingScreen.SetActive(true);
            while (!operation.isDone)
            {
                yield return null;
            }
        }
    }
}