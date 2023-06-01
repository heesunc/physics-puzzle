using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{ 
    [Header("---- [Core]")] // <- 오브젝트 풀링
    public int maxLevel;
    public bool isOver;
    public int score;
    
    [Header("---- [Object Pooling]")]
    public GameObject donglePrefab;
    public Transform dongleGroup;
    public GameObject effectPrefab;
    public Transform effectGroup;

    [Range(1, 30)] // 슬라이더로 변함
    public int poolSize;
    public List<Dongle> donglePool;
    public List<ParticleSystem> effectPool;
    Dongle lastDongle; // Dongle에서 안 쓰는거는 public빼기
    int poolCursor;

    [Header("------[Audio]")]
    public AudioSource bgmPlayer; // 배경음
    public AudioSource[] sfxPlayers; // 효과음
    public AudioClip[] sfxClips;
    int sfxCursor;

    [Header("------[UI]")]
    public GameObject line;
    public GameObject floor;
    public GameObject startGroup;
    public GameObject endGroup;
    public Text scoreText;
    public Text maxScoreText;
    public Text subScoreText;

    public enum Sfx { LevelUp, Next, Attach, Button, Over };

    void Awake()
    {
        // 프레임 설정 (FPS 60)
        Application.targetFrameRate = 60;

        // 오브젝트 풀 시작
        donglePool = new List<Dongle>();
        effectPool = new List<ParticleSystem>();
        for(int index=0; index < poolSize; index++){
            MakeDongle(index);
        }

        // 최대 점수 설정
        if(!PlayerPrefs.HasKey("MaxScore")){
            PlayerPrefs.SetInt("MaxScore",0);
        }
        maxScoreText.text = PlayerPrefs.GetInt("MaxScore").ToString();
    }

    Dongle MakeDongle(int id)
    {
        // 새로운 이펙트 생성 + 풀 저장
        GameObject instantEffect = Instantiate(effectPrefab, effectGroup);
        instantEffect.name = "Effect " + id;
        ParticleSystem instantEffectParticle = instantEffect.GetComponent<ParticleSystem>();
        effectPool.Add(instantEffectParticle);

        // 새로운 동글 생성 (생성 -> 레벨 생성 (프리팹가서 끄고) -> 활성화) + 풀 저장
        GameObject instantDongle = Instantiate(donglePrefab, dongleGroup);
        Dongle instantDongleLogic = instantDongle.GetComponent<Dongle>();
        instantDongle.name = "Dongle " + id;

        instantDongleLogic.manager = this;
        instantDongleLogic.effect = instantEffectParticle;

        donglePool.Add(instantDongleLogic);

        return instantDongleLogic;
    }

    Dongle GetDongle()
    {
        for(int index=0; index<donglePool.Count; index++){
            poolCursor = (poolCursor + 1) % donglePool.Count;
            if(!donglePool[poolCursor].gameObject.activeSelf){
                return donglePool[poolCursor];
            }
        }
        return MakeDongle(donglePool.Count);
    }

    public void GameStart()
    {
        // UI 컨트롤
        startGroup.SetActive(false);
        line.SetActive(true);
        floor.SetActive(true);
        scoreText.gameObject.SetActive(true);
        maxScoreText.gameObject.SetActive(true);

        // 효과음
        PlaySfx(Sfx.Button);

        // BGM 시작
        bgmPlayer.Play();

        // 동글 생성 시작
        Invoke("NextDongle", 1.5f); // Invoke는 함수를 느리게 호출해줌
    }

    void NextDongle()
    {
        if (isOver)
            return;
        
        // 다음 동글 가져오기
        lastDongle = GetDongle();
        lastDongle.level = Random.Range(0,maxLevel);
        lastDongle.gameObject.SetActive(true); 

        // 다음 동글 생성 기다리는 코루틴
        StartCoroutine("WaitNext"); // 문자열로 해도 되고, 그냥 해도 됨

        // 효과음 출력
        PlaySfx(Sfx.Next);
    }

    IEnumerator WaitNext() // 코르틴 Coroutine
    {
        // 현재 동글이 드랍될 때까지 기다리기
        while (lastDongle != null) {
            yield return null; // 이게 기본임, 업데이트가 1프레임 도는거랑 똑같음
        }
        yield return new WaitForSeconds(2.5f);
        
        // 다음 동글 생성 호출
        NextDongle();
    }

    public void TouchDown()
    {
        if (lastDongle == null)
            return;
        // 동글 드래그
        lastDongle.Drag();
    }

    public void TouchUp()
    {
        if (lastDongle == null) // 비어있으면 패스, 예외처리
            return;

        // 동글 드랍 (변수 비우기)
        lastDongle.Drop();
        lastDongle = null; // 한 번 썼으니가 필요가 없으니 null값
    }
    public void Result()
    {
        // 게임 오버 및 결산
        isOver = true;
        bgmPlayer.Stop();
        StartCoroutine("ResultRoutine");
    }

    IEnumerator ResultRoutine()
    {
        // 남아있는 동글을 순차적으로 지우면서 결산
        for(int index=0; index < donglePool.Count; index++){
            if(donglePool[index].gameObject.activeSelf){
                donglePool[index].Hide(Vector3.up * 100); // 따로 처리해줘야함
                yield return new WaitForSeconds(0.1f);
            }
        }
        yield return new WaitForSeconds(1f);
        // 점수 적용
        subScoreText.text = "점수 : " + scoreText.text;

        // 최대 점수 갱신
        int maxScore = Mathf.Max(PlayerPrefs.GetInt("MaxScore"), score);
        PlayerPrefs.SetInt("MaxScore", maxScore);

        // UI 띄우기
        endGroup.SetActive(true);

        // Over 효과음 출력
        PlaySfx(Sfx.Over);
    }

    public void Reset()
    {
        // 효과음 출력
        PlaySfx(Sfx.Button);
        StartCoroutine("ResetRoutine");
    }

    IEnumerator ResetRoutine()
    {
        yield return new WaitForSeconds(1f);
        // 장면 다시 불러오기
        SceneManager.LoadScene("SampleScene");
    }

    public void PlaySfx(Sfx type)
    {
        // SFX 플레이어 커서 이동
        sfxCursor = (sfxCursor + 1) % sfxPlayers.Length;

        // 효과음 사운드 지정
        switch(type){
            case Sfx.LevelUp:
                sfxPlayers[sfxCursor].clip = sfxClips[Random.Range(0,3)];
                break;
            case Sfx.Next:
                sfxPlayers[sfxCursor].clip = sfxClips[3];
                break;
            case Sfx.Attach:
                sfxPlayers[sfxCursor].clip = sfxClips[4];
                break;
            case Sfx.Button:
                sfxPlayers[sfxCursor].clip = sfxClips[5];
                break;
            case Sfx.Over:
                sfxPlayers[sfxCursor].clip = sfxClips[6];
                break;
        }
        // 효과음 플레이
        sfxPlayers[sfxCursor].Play();
    }

    void LateUpdate()
    {
        scoreText.text = score.ToString();
    }
}
